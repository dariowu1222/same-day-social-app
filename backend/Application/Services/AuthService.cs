using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;
using SameDaySocialApp.Domain.Enums;

namespace SameDaySocialApp.Application.Services;

public sealed class AuthService
{
    private readonly JsonStorageService storage;
    private readonly IEmailSender emailSender;
    private readonly AppDbContext? db;
    private readonly AccountStateService? accountState;
    private readonly EmailSendThrottle? emailThrottle;

    public AuthService(JsonStorageService storage, IEmailSender emailSender, IServiceProvider services)
    {
        this.storage = storage;
        this.emailSender = emailSender;
        db = services.GetService<AppDbContext>();
        accountState = services.GetService<AccountStateService>();
        emailThrottle = services.GetService<EmailSendThrottle>();
    }

    public async Task<AuthServiceResult<RegistrationStartResponse>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法建立正式帳號");
        }

        var validation = ValidateRegister(command);
        if (validation != null)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail(validation.Value.Code, validation.Value.Message);
        }

        if (!command.TermsAccepted)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("TERMS_NOT_ACCEPTED", "請先閱讀並同意服務條款與隱私權政策");
        }

        var email = NormalizeEmail(command.Email);
        var exists = await db.AuthAccounts.AnyAsync(x => x.Username == email, cancellationToken);
        if (exists)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("ACCOUNT_EXISTS", "這個 Email 已經註冊過，請直接登入或使用忘記密碼");
        }

        // 同一個信箱的寄信冷卻。每 IP 每分鐘 10 次擋不住換 IP 繼續寄——
        // 對收件人是信箱被灌爆，對這個服務更糟：寄信走 Gmail SMTP，有每日上限，
        // 額度被耗光之後所有人的註冊信與重設密碼信都寄不出去。
        var now = DateTimeOffset.UtcNow;
        if (emailThrottle != null && !emailThrottle.TryStartSend(email, now))
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail(
                "EMAIL_COOLDOWN", "驗證碼剛寄出，請稍候再試一次");
        }

        var code = AuthSecurity.CreateVerificationCode();
        var token = new RegistrationVerificationTokenRecord
        {
            Id = $"reg_{Guid.NewGuid():N}",
            Email = email,
            Nickname = command.Nickname.Trim(),
            PasswordHash = AuthSecurity.HashPassword(command.Password),
            // 欄位叫 birth_year 是歷史因素：以前註冊只問出生年次。新版送完整的出生年月日，
            // 就把 yyyy-MM-dd 存在同一欄（長度 40，放得下），驗證完成時再讀回來寫進
            // users.birthday；舊版 App 仍然只送年次，那就照舊存四位數的年。
            // 不另外加欄位，是因為那要改 schema，而這一欄只在這裡寫、在驗證完成時讀。
            BirthYear = BirthdayOrYearOf(command),
            Gender = NormalizeGender(command.Gender),
            CodeHash = AuthSecurity.HashVerificationCode(code),
            TermsAcceptedAt = now,
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now
        };

        var oldTokens = await db.RegistrationVerificationTokens
            .Where(x => x.Email == email && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var oldToken in oldTokens)
        {
            oldToken.UsedAt = now;
        }
        db.RegistrationVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendRegistrationVerificationCodeAsync(email, code, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SMTP_NOT_CONFIGURED")
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            emailThrottle?.ReleaseSend(email);
            return AuthServiceResult<RegistrationStartResponse>.Fail("SMTP_NOT_CONFIGURED", "尚未設定寄信服務，無法寄出建立帳號驗證碼");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            // 沒寄出去的信不該佔用冷卻，不然 SMTP 一時不通就得等滿一分鐘才能再試
            emailThrottle?.ReleaseSend(email);
            return AuthServiceResult<RegistrationStartResponse>.Fail("EMAIL_SEND_FAILED", "驗證碼寄送失敗，請稍後再試或檢查寄信設定");
        }

        return AuthServiceResult<RegistrationStartResponse>.Ok(new RegistrationStartResponse(email, 10));
    }

    public async Task<AuthServiceResult<AuthUserResponse>> ConfirmRegistrationAsync(ConfirmRegistrationCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法建立正式帳號");
        }

        var email = NormalizeEmail(command.Email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(command.Code))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CODE", "請輸入正確的 Email 與驗證碼");
        }

        var exists = await db.AuthAccounts.AnyAsync(x => x.Username == email, cancellationToken);
        if (exists)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_EXISTS", "這個 Email 已經註冊過，請直接登入或使用忘記密碼");
        }

        var now = DateTimeOffset.UtcNow;
        var tokens = await db.RegistrationVerificationTokens
            .Where(x => x.Email == email && x.UsedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);
        var token = tokens.FirstOrDefault(x => AuthSecurity.VerifyVerificationCode(command.Code, x.CodeHash));
        if (token == null)
        {
            await CountFailedAttemptAsync(tokens, t => t.FailedAttempts, (t, n) => t.FailedAttempts = n,
                (t, at) => t.UsedAt = at, cancellationToken);
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CODE", "驗證碼錯誤或已過期");
        }

        // token.BirthYear 是完整的出生年月日（新版）或只有年次（舊版 App），見 RegisterAsync。
        // 只有年次的人 birthday 會是空的，等他更新 App 之後由補問畫面補上。
        var birthday = AgeRules.ParseBirthday(token.BirthYear);
        var user = new UserRecord
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = token.Nickname,
            Birthday = birthday,
            AgeRange = birthday?.Year.ToString() ?? token.BirthYear,
            Gender = token.Gender,
            Bio = "今天開始慢慢認識同頻的人",
            CreatedAt = now,
            UpdatedAt = now
        };
        var account = new AuthAccountRecord
        {
            Id = $"auth_{Guid.NewGuid():N}",
            UserId = user.Id,
            Username = token.Email,
            PasswordHash = token.PasswordHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        token.UsedAt = now;
        db.Users.Add(user);
        db.AuthAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, account.Username));
    }

    /// <summary>
    /// 這顆帳號現在能不能走進來（登入、社群登入、重設密碼都算）。可以就回那列
    /// users，不行回 null——呼叫端拿到 null 就是擋下，不必再自己判斷一次。
    ///
    /// 為什麼不看 auth_accounts.is_disabled：
    /// 那是處置當下寫下的快照，不會隨時間變。但停權是有期限的，期限一到人就該
    /// 回來，而把 is_disabled 解開的 RestoreExpiredSuspensions 要等排程跑過才會動它。
    /// 中間那段時間同一個人會得到兩個答案：舊 token 還能用（請求閘門看的是
    /// users.status，它自己會算期限），重新登入卻被擋下，畫面上只說「請聯絡客服」。
    /// token 一過期他就徹底進不來，而且沒有任何地方告訴他為什麼。
    ///
    /// 所以登入這一側也改成問 users.status / suspended_until，跟請求閘門、列表
    /// 過濾走同一個 AccountStateService.IsActive。is_disabled 從此只是那份狀態的
    /// 鏡子（仍然會同步寫，後台和直接查資料庫的人靠它看得懂），不再是另一份真相。
    ///
    /// 這不會把洞開大：封鎖與註銷沒有期限，IsActive 永遠回 false。現在唯一能
    /// 靠時間自己回來的只有「設了期限而且已經到期」的停權。
    /// </summary>
    private async Task<UserRecord?> LoadSignInUserAsync(AuthAccountRecord account, CancellationToken cancellationToken)
    {
        var user = await db!.Users.FirstOrDefaultAsync(x => x.Id == account.UserId, cancellationToken);

        // 查無此人：auth_accounts 還在但 users 那列不見了，沒有理由發 token 出去
        return user != null
               && AccountStateService.IsActive(user.Status, user.SuspendedUntil, DateTimeOffset.UtcNow)
            ? user
            : null;
    }

    public async Task<AuthServiceResult<AuthUserResponse>> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法登入正式帳號");
        }

        var email = NormalizeEmail(command.Email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_LOGIN", "請輸入正確的 Email 與密碼");
        }

        AuthAccountRecord? account;
        UserRecord? user;
        try
        {
            account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
            user = account == null ? null : await LoadSignInUserAsync(account, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }

        // 去帳號枚舉：帳號不存在與密碼錯誤回覆一致，避免探測 Email 是否註冊
        if (account == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CREDENTIALS", "Email 或密碼錯誤，請重新確認");
        }

        if (user == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_DISABLED", "此帳號目前無法登入，請聯絡客服或稍後再試");
        }

        if (!AuthSecurity.VerifyPassword(command.Password, account.PasswordHash))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CREDENTIALS", "Email 或密碼錯誤，請重新確認");
        }

        try
        {
            account.LastLoginAt = DateTimeOffset.UtcNow;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, account.Username));
    }

    // Google 登入：用 Google 已驗證的 email 找帳號，沒有就建一個（無密碼，不能用密碼登入）。
    public Task<AuthServiceResult<AuthUserResponse>> GoogleLoginAsync(GoogleLoginCommand command, CancellationToken cancellationToken)
        => SocialLoginAsync(command.Email, command.Name, command.TermsAccepted, command.Birthday, cancellationToken);

    public Task<AuthServiceResult<AuthUserResponse>> FacebookLoginAsync(FacebookLoginCommand command, CancellationToken cancellationToken)
        => SocialLoginAsync(command.Email, command.Name, command.TermsAccepted, command.Birthday, cancellationToken);

    /// <summary>
    /// Google / Facebook 共用：以 email 為主鍵建立或登入帳號（provider 無關）。
    ///
    /// 同一支端點其實做兩件事，規則不一樣：
    /// · 這個 email 已經有帳號 → 登入。不看 termsAccepted／birthday。
    /// · 沒有帳號 → 註冊。必須帶著「同意條款」與「成年的出生年月日」才建，
    ///   否則回 SOCIAL_CONSENT_REQUIRED，前端收到就顯示確認畫面、帶著同一張憑證再打一次。
    ///
    /// 以前第二種情況是直接建帳號：按一下「使用 Google 繼續」就進來了，
    /// 沒有同意條款、沒有年齡限制，而 Email 註冊兩樣都要。同一個服務兩個入口、
    /// 兩套標準，未滿 18 歲的人改按 Google 就繞過去了。
    ///
    /// 登入不看那兩個欄位是刻意的：已上架的版本不會送它們。如果連登入都要求，
    /// 等於把所有還沒更新 App 的既有使用者鎖在門外。
    /// </summary>
    private async Task<AuthServiceResult<AuthUserResponse>> SocialLoginAsync(
        string emailRaw, string name, bool termsAccepted, string? birthday, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法登入");
        }

        var email = NormalizeEmail(emailRaw);
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_SOCIAL_ACCOUNT", "無法取得有效的社群帳號 Email");
        }

        var now = DateTimeOffset.UtcNow;

        AuthAccountRecord? account;
        try
        {
            account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }

        // 既有帳號（含原本用 Email 密碼註冊的同 email）→ 直接登入
        if (account != null)
        {
            UserRecord? existingUser;
            try
            {
                existingUser = await LoadSignInUserAsync(account, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
            }

            // 社群登入跟 Email 密碼登入擋的是同一件事。少了這一關，被封鎖的人
            // 只要改用 Google 按一下就繞過去了——同一個 email、同一個帳號。
            if (existingUser == null)
            {
                return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_DISABLED", "此帳號目前無法登入，請聯絡客服或稍後再試");
            }

            try
            {
                account.LastLoginAt = now;
                account.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(existingUser.Id, existingUser.Nickname, account.Username));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
            }
        }

        // 從這裡開始是「註冊」。跟 Email 註冊同一套門檻：先同意條款，再確認成年。
        //
        // 兩樣缺一樣都回同一個碼——前端的反應是一樣的：顯示確認畫面。
        // 這段訊息新版前端不會顯示（它認的是碼），會看到它的是還沒更新的舊版 App，
        // 所以寫給舊版的人看：告訴他怎麼辦，而不是告訴他缺了哪個欄位。
        if (!termsAccepted || string.IsNullOrWhiteSpace(birthday))
        {
            return AuthServiceResult<AuthUserResponse>.Fail(
                "SOCIAL_CONSENT_REQUIRED",
                "第一次用這個帳號登入，需要先同意服務條款並確認年齡。請更新 App 後再試一次，或改用 Email 建立帳號");
        }

        var refusal = RefusalFor(birthday, out var birthDate);
        if (refusal != null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail(refusal.Value.Code, refusal.Value.Message);
        }

        // 首次用社群登入 → 建立新帳號（密碼設成不可用的隨機 hash）
        var nickname = string.IsNullOrWhiteSpace(name) ? "同頻使用者" : name.Trim();
        if (nickname.Length > 20)
        {
            nickname = nickname[..20];
        }

        var user = new UserRecord
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = nickname,
            // 生日是配對年齡篩選比對的欄位，也是卡片上年齡與星座的來源。
            // age_range 只留年次：它同時是「這個帳號走過同意流程」的記號（見 IsConsentMissingAsync）。
            Birthday = birthDate,
            AgeRange = birthDate.Year.ToString(),
            Bio = "今天開始慢慢認識同頻的人",
            CreatedAt = now,
            UpdatedAt = now
        };
        var newAccount = new AuthAccountRecord
        {
            Id = $"auth_{Guid.NewGuid():N}",
            UserId = user.Id,
            Username = email,
            PasswordHash = AuthSecurity.HashPassword(Guid.NewGuid().ToString("N")), // 佔位，無法用於密碼登入
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            db.Users.Add(user);
            db.AuthAccounts.Add(newAccount);
            db.RegistrationVerificationTokens.Add(ConsentRecord(newAccount, user, now));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, newAccount.Username));
    }

    /// <summary>
    /// 一筆「這個帳號在什麼時候同意了條款、報的出生年月日是哪一天」的紀錄。
    ///
    /// Email 註冊的同意時間記在 registration_verification_tokens 那一列上
    /// （驗證完只是標記 UsedAt，列會留著），那張表實際上就是註冊紀錄。
    /// 社群註冊與事後補問都沒有驗證碼這一步，但同意的事實一樣要留下來，所以寫一列
    /// 「一建立就已經用掉」的紀錄進同一張表——查誰在什麼時候同意過，只要看一個地方，
    /// 也不必為此改 schema。
    ///
    /// UsedAt 與 ExpiresAt 都是現在，驗證碼那條路（只找未用、未過期的）永遠撈不到它；
    /// CodeHash 是一段沒有人知道原文的亂數。
    /// </summary>
    private static RegistrationVerificationTokenRecord ConsentRecord(AuthAccountRecord account, UserRecord user, DateTimeOffset now)
        => new()
        {
            Id = $"reg_{Guid.NewGuid():N}",
            Email = account.Username,
            Nickname = user.Nickname,
            PasswordHash = account.PasswordHash,
            BirthYear = user.Birthday != null ? AgeRules.Format(user.Birthday.Value) : user.AgeRange,
            CodeHash = AuthSecurity.HashVerificationCode(Guid.NewGuid().ToString("N")),
            TermsAcceptedAt = now,
            ExpiresAt = now,
            UsedAt = now,
            CreatedAt = now
        };

    /// <summary>
    /// 這個帳號還缺不缺「同意條款」與「出生年月日」。
    ///
    /// 會缺的有兩種人：
    /// · 以前用 Google／Facebook 按一下就建好的帳號——那條路當時什麼都沒問。
    /// · 用舊版 App 以 Email 註冊的帳號——同意過條款，但當時只問出生年次，沒有完整的生日。
    /// 新的註冊（兩種方式都是）已經在建立帳號之前問齊了，但已經存在的帳號不會再走到那裡。
    ///
    /// 這裡只回答「缺不缺」，不擋任何事。擋在登入或每一支 API 上的話，還在用舊版 App 的人
    /// 會被整個鎖在外面，而舊版根本沒有畫面可以讓他補。所以由新版 App 在登入後自己問、
    /// 自己顯示確認畫面；這支端點是它判斷要不要顯示的依據。
    /// </summary>
    public async Task<AuthServiceResult<ConsentStatus>> GetConsentStatusAsync(string userId, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            // JSON 模式沒有登入、沒有帳號，沒有人需要被問
            return AuthServiceResult<ConsentStatus>.Ok(new ConsentStatus(false));
        }

        try
        {
            var (account, user) = await LoadAccountAsync(userId, cancellationToken);
            if (account == null || user == null)
            {
                return AuthServiceResult<ConsentStatus>.Fail("ACCOUNT_NOT_FOUND", "找不到帳號");
            }

            return AuthServiceResult<ConsentStatus>.Ok(
                new ConsentStatus(await IsConsentMissingAsync(account, user, cancellationToken)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<ConsentStatus>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }
    }

    /// <summary>
    /// 既有帳號補上同意與出生年月日。門檻跟註冊一樣：要勾同意、要是成年的生日。
    ///
    /// 報的生日未成年時只是不放行，不對帳號做任何處置——這是自己填的日期，
    /// 選錯一格也會落在這裡，而他在註冊時本來就能重填。要不要對確定未成年的帳號
    /// 做什麼，是審核的事，不該由一個輸入框順手決定。
    /// </summary>
    public async Task<AuthServiceResult<ConsentStatus>> RecordConsentAsync(
        string userId, bool termsAccepted, string? birthday, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<ConsentStatus>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線");
        }

        if (!termsAccepted)
        {
            return AuthServiceResult<ConsentStatus>.Fail("TERMS_NOT_ACCEPTED", "請先閱讀並同意服務條款與隱私權政策");
        }

        var refusal = RefusalFor(birthday, out var birthDate);
        if (refusal != null)
        {
            return AuthServiceResult<ConsentStatus>.Fail(refusal.Value.Code, refusal.Value.Message);
        }

        try
        {
            var (account, user) = await LoadAccountAsync(userId, cancellationToken);
            if (account == null || user == null)
            {
                return AuthServiceResult<ConsentStatus>.Fail("ACCOUNT_NOT_FOUND", "找不到帳號");
            }

            // 已經齊了就什麼都不寫。同一個人連按兩次、或兩台裝置各問一次，
            // 都不該多出一列紀錄，更不該把第一次同意的時間蓋掉。
            if (!await IsConsentMissingAsync(account, user, cancellationToken))
            {
                return AuthServiceResult<ConsentStatus>.Ok(new ConsentStatus(false));
            }

            var now = DateTimeOffset.UtcNow;
            // 這次確認的日期就是他的生日，年次跟著它走。原本在個人檔案填過別的日期也以這次為準：
            // 這是他對著「年滿 18 歲」那句話親手填的，而個人檔案那一格之後照樣能改。
            user.Birthday = birthDate;
            user.AgeRange = birthDate.Year.ToString();
            user.UpdatedAt = now;

            db.RegistrationVerificationTokens.Add(ConsentRecord(account, user, now));
            await db.SaveChangesAsync(cancellationToken);
            return AuthServiceResult<ConsentStatus>.Ok(new ConsentStatus(false));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<ConsentStatus>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試");
        }
    }

    private async Task<(AuthAccountRecord? Account, UserRecord? User)> LoadAccountAsync(string userId, CancellationToken cancellationToken)
    {
        var account = await db!.AuthAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return (account, user);
    }

    /// <summary>
    /// 三樣都要有才算齊：生日、出生年次（age_range）、一列已經結掉的註冊／同意紀錄。
    ///
    /// 生日（users.birthday）：配對的年齡篩選比的是它。沒有生日的人，對所有設了年齡範圍的人
    /// 都是隱形的——而 onboarding 以前會替每個人寫入一組年齡範圍，所以實際上是對幾乎所有人隱形。
    ///
    /// 年次（users.age_range）：它是「走過同意流程」的記號。只有三個地方會寫它——Email 註冊
    /// 驗證完成、社群註冊、這裡的補問——三處都在同一筆交易裡留下（或結掉）一列同意紀錄。
    /// 生日不能拿來當這個記號：個人檔案頁本來就能填生日，以前用社群登入建的帳號可能有生日、
    /// 卻從來沒同意過任何東西。
    ///
    /// 紀錄那一半單獨看是不夠的，別把它當成同意的證明：UsedAt 有值也可能只是作廢
    /// （重寄驗證碼會把舊的結掉、猜錯太多次也會），那種列上的 TermsAcceptedAt
    /// 是「有人拿這個 email 填過註冊表單」，不是帳號主人的同意。沒結掉的列更不算。
    /// 要單靠這張表分辨「完成」與「作廢」得加欄位，目前靠年次這一半擋住。
    /// </summary>
    private async Task<bool> IsConsentMissingAsync(AuthAccountRecord account, UserRecord user, CancellationToken cancellationToken)
    {
        if (user.Birthday == null || string.IsNullOrWhiteSpace(user.AgeRange))
        {
            return true;
        }

        var agreed = await db!.RegistrationVerificationTokens
            .AnyAsync(x => x.Email == account.Username && x.UsedAt != null, cancellationToken);
        return !agreed;
    }

    // 軟刪除帳號：停用登入並清除個資／媒體（保留列；真正硬清除另排）
    public bool DeleteAccount(string userId)
    {
        if (db != null)
        {
            var account = db.AuthAccounts.FirstOrDefault(x => x.UserId == userId);
            var user = db.Users.FirstOrDefault(x => x.Id == userId);
            if (account == null && user == null) return false;
            if (account != null)
            {
                account.IsDisabled = true;
                account.UpdatedAt = DateTimeOffset.UtcNow;
            }
            if (user != null)
            {
                user.Nickname = "已離開的使用者";
                user.Bio = "";
                user.PhotoDataUrls = [];
                // users.status 也要改。auth_accounts.is_disabled 只有登入路徑會讀，
                // 而每個已認證請求是看 users.status（見 AccountStateService 與
                // Program.cs 的帳號狀態中介軟體）。先前只設 is_disabled，等於
                // 自助註銷之後手上那顆 token 還能用到過期為止——最長七天，期間
                // 照樣讀對話、傳訊息，只是暱稱變成「已離開的使用者」。
                // 後台封鎖那條路（ChangeStatus）早就處理了，自助這條漏掉。
                user.Status = AccountStatus.DELETED.ToString();
                user.UpdatedAt = DateTimeOffset.UtcNow;
            }
            db.SaveChanges();
            // 快取有 60 秒，不清的話註銷完還有一分鐘的空窗
            accountState?.Invalidate(userId);
            return true;
        }

        var existing = storage.ReadCollection<User>("users").FirstOrDefault(x => x.Id == userId);
        if (existing == null) return false;
        storage.UpdateOne<User>("users", userId, u =>
        {
            u.Nickname = "已離開的使用者";
            u.Bio = "";
            u.PhotoDataUrls = [];
            u.Status = AccountStatus.DELETED;
        });
        return true;
    }

    // 封鎖名單（安全中心）：列出我封鎖的人 + 解除封鎖
    public List<BlockedUser> GetBlockedUsers(string userId)
    {
        if (db != null)
        {
            var blocks = db.UserBlocks.AsNoTracking().Where(b => b.BlockerId == userId).ToList();
            var ids = blocks.Select(b => b.BlockedId).ToList();
            var names = db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u.Nickname);
            return blocks
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlockedUser
                {
                    UserId = b.BlockedId,
                    Nickname = names.GetValueOrDefault(b.BlockedId, "使用者"),
                    BlockedAt = b.CreatedAt,
                })
                .ToList();
        }

        return storage.ReadCollection<UserBlockRecord>("userBlocks")
            .Where(b => b.BlockerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlockedUser { UserId = b.BlockedId, Nickname = "使用者", BlockedAt = b.CreatedAt })
            .ToList();
    }

    public bool Unblock(string blockerId, string blockedId)
    {
        if (db != null)
        {
            var rows = db.UserBlocks.Where(b => b.BlockerId == blockerId && b.BlockedId == blockedId).ToList();
            if (rows.Count == 0) return false;
            db.UserBlocks.RemoveRange(rows);
            db.SaveChanges();
            return true;
        }

        var blocks = storage.ReadCollection<UserBlockRecord>("userBlocks");
        var match = blocks.FirstOrDefault(b => b.BlockerId == blockerId && b.BlockedId == blockedId);
        if (match == null) return false;
        blocks.Remove(match);
        storage.WriteCollection("userBlocks", blocks);
        return true;
    }

    // 隱私 / 通知設定
    public UserSetting GetSettings(string userId)
    {
        if (db != null)
        {
            var rec = db.UserSettings.AsNoTracking().FirstOrDefault(x => x.UserId == userId);
            return rec == null ? new UserSetting() : ToSetting(rec);
        }
        var item = storage.ReadCollection<UserSettingRecord>("userSettings").FirstOrDefault(x => x.UserId == userId);
        return item == null ? new UserSetting() : ToSetting(item);
    }

    public UserSetting UpdateSettings(string userId, bool? profilePublic, bool? pauseMatching, bool? notifyMatch, bool? notifyMessage, bool? notifyRant)
    {
        void Apply(UserSettingRecord r)
        {
            if (profilePublic.HasValue) r.ProfilePublic = profilePublic.Value;
            if (pauseMatching.HasValue) r.PauseMatching = pauseMatching.Value;
            if (notifyMatch.HasValue) r.NotifyMatch = notifyMatch.Value;
            if (notifyMessage.HasValue) r.NotifyMessage = notifyMessage.Value;
            if (notifyRant.HasValue) r.NotifyRant = notifyRant.Value;
            r.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (db != null)
        {
            var rec = db.UserSettings.FirstOrDefault(x => x.UserId == userId);
            if (rec == null) { rec = new UserSettingRecord { UserId = userId }; Apply(rec); db.UserSettings.Add(rec); }
            else Apply(rec);
            db.SaveChanges();
            return ToSetting(rec);
        }

        var list = storage.ReadCollection<UserSettingRecord>("userSettings");
        var item = list.FirstOrDefault(x => x.UserId == userId);
        if (item == null) { item = new UserSettingRecord { UserId = userId }; Apply(item); list.Add(item); }
        else Apply(item);
        storage.WriteCollection("userSettings", list);
        return ToSetting(item);
    }

    private static UserSetting ToSetting(UserSettingRecord r) => new()
    {
        ProfilePublic = r.ProfilePublic,
        PauseMatching = r.PauseMatching,
        NotifyMatch = r.NotifyMatch,
        NotifyMessage = r.NotifyMessage,
        NotifyRant = r.NotifyRant,
    };

    public async Task<AuthServiceResult<PasswordResetRequestResponse>> RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法重設密碼");
        }

        email = NormalizeEmail(email);
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("INVALID_EMAIL", "請輸入正確的 Email");
        }

        // 冷卻要在查帳號之前。這支刻意對「查無此帳號」回覆成功（去帳號枚舉），
        // 所以冷卻如果只套用在帳號存在的那條路上，就等於給了一個新的判斷方式：
        // 同一個信箱連打兩次，會被擋下來的就是註冊過的。
        // 同理，查無此帳號的那條路也照樣消耗額度，不歸還。
        if (emailThrottle != null && !emailThrottle.TryStartSend(email, DateTimeOffset.UtcNow))
        {
            return AuthServiceResult<PasswordResetRequestResponse>.Fail(
                "EMAIL_COOLDOWN", "驗證碼剛寄出，請稍候再試一次");
        }

        var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        if (account == null || await LoadSignInUserAsync(account, cancellationToken) == null)
        {
            // 去帳號枚舉：不洩漏 Email 是否註冊，一律回覆「已寄出（若該帳號存在）」
            return AuthServiceResult<PasswordResetRequestResponse>.Ok(new PasswordResetRequestResponse(email, 10));
        }

        var code = AuthSecurity.CreateVerificationCode();
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetTokenRecord
        {
            Id = $"reset_{Guid.NewGuid():N}",
            UserId = account.UserId,
            CodeHash = AuthSecurity.HashVerificationCode(code),
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now
        };
        // 先把這個人手上還沒用掉的碼全部作廢。註冊那邊一直有做，這邊沒有——
        // 所以可以先申請五次讓五組碼同時有效，猜中的機率直接乘五。
        var oldTokens = await db.PasswordResetTokens
            .Where(x => x.UserId == account.UserId && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var oldToken in oldTokens)
        {
            oldToken.UsedAt = now;
        }

        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendPasswordResetCodeAsync(email, code, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SMTP_NOT_CONFIGURED")
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("SMTP_NOT_CONFIGURED", "尚未設定 SMTP，無法寄出驗證碼。請先設定寄信帳號");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("EMAIL_SEND_FAILED", "驗證碼寄送失敗，請稍後再試或檢查 SMTP 設定");
        }

        return AuthServiceResult<PasswordResetRequestResponse>.Ok(new PasswordResetRequestResponse(email, 10));
    }

    public async Task<AuthServiceResult<PasswordResetVerifyResponse>> VerifyPasswordResetCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        var tokenResult = await FindValidResetTokenAsync(email, code, cancellationToken);
        if (!tokenResult.Success)
        {
            return AuthServiceResult<PasswordResetVerifyResponse>.Fail(tokenResult.Code!, tokenResult.Message!);
        }

        return AuthServiceResult<PasswordResetVerifyResponse>.Ok(new PasswordResetVerifyResponse(true));
    }

    public async Task<AuthServiceResult<AuthUserResponse>> ConfirmPasswordResetAsync(ConfirmPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var passwordValidation = ValidatePassword(command.NewPassword, command.ConfirmPassword);
        if (passwordValidation != null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail(passwordValidation.Value.Code, passwordValidation.Value.Message);
        }

        var tokenResult = await FindValidResetTokenAsync(command.Email, command.Code, cancellationToken);
        if (!tokenResult.Success)
        {
            return AuthServiceResult<AuthUserResponse>.Fail(tokenResult.Code!, tokenResult.Message!);
        }

        var (account, user, token) = tokenResult.Data!;
        account.PasswordHash = AuthSecurity.HashPassword(command.NewPassword);
        account.UpdatedAt = DateTimeOffset.UtcNow;
        token.UsedAt = DateTimeOffset.UtcNow;
        await db!.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, account.Username));
    }

    private async Task<AuthServiceResult<(AuthAccountRecord Account, UserRecord User, PasswordResetTokenRecord Token)>> FindValidResetTokenAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法重設密碼");
        }

        email = NormalizeEmail(email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(code))
        {
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "請輸入正確的 Email 與驗證碼");
        }

        var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        var user = account == null ? null : await LoadSignInUserAsync(account, cancellationToken);
        if (account == null || user == null)
        {
            // 去帳號枚舉：與「驗證碼錯誤」回覆一致
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "驗證碼錯誤或已過期");
        }

        var now = DateTimeOffset.UtcNow;
        var tokens = await db.PasswordResetTokens
            .Where(x => x.UserId == account.UserId && x.UsedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);
        var token = tokens.FirstOrDefault(x => AuthSecurity.VerifyVerificationCode(code, x.CodeHash));
        if (token == null)
        {
            await CountFailedAttemptAsync(tokens, t => t.FailedAttempts, (t, n) => t.FailedAttempts = n,
                (t, at) => t.UsedAt = at, cancellationToken);
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "驗證碼錯誤或已過期");
        }

        return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Ok((account, user, token));
    }

    /// <summary>
    /// 猜錯一次。到上限就把那組碼作廢（UsedAt 一填，後續查詢就不會再撈到它）。
    ///
    /// 為什麼要記在碼身上而不是只靠 IP 限流：碼是 6 位數、有效 10 分鐘，
    /// 每 IP 每分鐘 10 次擋不住「一千個 IP 一起猜」——那樣十分鐘內大約有四成
    /// 的機率猜中，也就是直接重設掉別人的密碼。
    ///
    /// 手上這批理論上只會有一組（申請新碼時會作廢舊的），但舊資料可能有好幾組，
    /// 所以整批一起算：一次猜測本來就是對整批都試過一遍。
    /// </summary>
    private async Task CountFailedAttemptAsync<T>(
        IReadOnlyCollection<T> tokens,
        Func<T, int> readAttempts,
        Action<T, int> writeAttempts,
        Action<T, DateTimeOffset> markUsed,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            var attempts = readAttempts(token) + 1;
            writeAttempts(token, attempts);
            if (attempts >= AuthSecurity.MaxCodeAttempts)
            {
                markUsed(token, now);
            }
        }
        await db!.SaveChangesAsync(cancellationToken);
    }

    private static (string Code, string Message)? ValidateRegister(RegisterCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Nickname) || command.Nickname.Trim().Length is < 2 or > 20)
        {
            return ("INVALID_NICKNAME", "暱稱需為 2 到 20 個字");
        }

        if (!IsValidEmail(NormalizeEmail(command.Email)))
        {
            return ("INVALID_EMAIL", "請輸入可以收信的 Email");
        }

        // 新版送完整的出生年月日，那就照完整的日期判斷滿不滿 18 歲。
        // 已上架的舊版 App 只有「出生年次」一格，送不出日期；那些請求照舊用年次判斷——
        // 連 Email 註冊都要求日期的話，舊版上就完全沒有辦法建立帳號了。
        // 用舊版註冊的人生日會是空的，等他更新之後由補問畫面補上（見 IsConsentMissingAsync）。
        if (!string.IsNullOrWhiteSpace(command.Birthday))
        {
            var refusal = RefusalFor(command.Birthday, out _);
            if (refusal != null)
            {
                return refusal;
            }
        }
        else if (!IsAdultBirthYear(command.BirthYear))
        {
            return ("UNDERAGE", "未滿 18 歲暫時不能使用同頻 Today");
        }

        return ValidatePassword(command.Password, command.ConfirmPassword);
    }

    /// <summary>
    /// 這個出生年月日過不過得了門檻。過了就把日期交出來；不過就回要給使用者看的理由。
    /// 註冊、社群登入的確認、既有帳號的補問，三個地方用的是同一道門檻。
    /// </summary>
    private static (string Code, string Message)? RefusalFor(string? birthday, out DateOnly birthDate)
    {
        var parsed = AgeRules.ParseBirthday(birthday);
        birthDate = parsed ?? default;
        if (parsed == null)
        {
            return ("INVALID_BIRTHDAY", "請選擇正確的出生年月日");
        }

        return AgeRules.IsAdult(parsed.Value)
            ? null
            : ("UNDERAGE", "未滿 18 歲暫時不能使用同頻 Today");
    }

    /// <summary>註冊紀錄上要記的出生資料：有完整生日就記生日，舊版只給年次就記年次。</summary>
    private static string? BirthdayOrYearOf(RegisterCommand command)
    {
        var birthday = AgeRules.ParseBirthday(command.Birthday);
        return birthday != null ? AgeRules.Format(birthday.Value) : NormalizeBirthYear(command.BirthYear);
    }

    private static (string Code, string Message)? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 64)
        {
            return ("INVALID_PASSWORD", "密碼需為 8 到 64 碼");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return ("WEAK_PASSWORD", "密碼需同時包含英文與數字");
        }

        if (password != confirmPassword)
        {
            return ("PASSWORD_NOT_MATCH", "兩次輸入的密碼不一致");
        }

        return null;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeBirthYear(string birthYear)
    {
        var normalized = new string(birthYear.Where(char.IsDigit).ToArray());
        if (normalized.Length >= 4)
        {
            normalized = normalized[..4];
        }

        if (!int.TryParse(normalized, out var year))
        {
            return null;
        }

        var currentYear = DateTimeOffset.Now.Year;
        return year >= 1940 && year <= currentYear - 18 ? year.ToString() : null;
    }

    private static bool IsAdultBirthYear(string birthYear)
    {
        var normalized = new string(birthYear.Where(char.IsDigit).ToArray());
        if (normalized.Length < 4 || !int.TryParse(normalized[..4], out var year))
        {
            return false;
        }

        var currentYear = DateTimeOffset.Now.Year;
        return year >= 1940 && year <= currentYear - 18;
    }

    private static string? NormalizeGender(string gender)
    {
        return gender is "MALE" or "FEMALE" or "PRIVATE" ? gender : "PRIVATE";
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Length <= 120;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record RegisterCommand(
    string Nickname,
    string Email,
    string Password,
    string ConfirmPassword,
    string BirthYear,
    string Gender,
    bool TermsAccepted,
    // 完整的出生年月日（yyyy-MM-dd）。新版一定會送；舊版 App 沒有這一欄，只有上面的 BirthYear。
    string? Birthday = null);

public sealed record LoginCommand(string Email, string Password);

// TermsAccepted／Birthday 只在「這個 email 還沒有帳號」時才會被看（見 SocialLoginAsync）。
// 有預設值是因為已上架的版本不會送它們；那些人登入既有帳號不該受影響。
// Birthday 是 yyyy-MM-dd 的出生年月日。
public sealed record GoogleLoginCommand(string Email, string Name, bool TermsAccepted = false, string? Birthday = null);

public sealed record FacebookLoginCommand(string Email, string Name, bool TermsAccepted = false, string? Birthday = null);

public sealed record ConfirmRegistrationCommand(string Email, string Code);

public sealed record ConfirmPasswordResetCommand(
    string Email,
    string Code,
    string NewPassword,
    string ConfirmPassword);

public sealed record AuthUserResponse(string UserId, string Nickname, string Email, string? Token = null);

/// <summary>
/// 這個帳號還缺不缺「同意條款」與「年齡確認」。Required 為 true 時，新版 App 會在登入後顯示確認畫面。
/// </summary>
public sealed record ConsentStatus(bool Required);

public sealed record RegistrationStartResponse(string Email, int ExpiresInMinutes);

public sealed record PasswordResetRequestResponse(string Email, int ExpiresInMinutes);

public sealed record PasswordResetVerifyResponse(bool Verified);

public sealed class AuthServiceResult<T>
{
    public bool Success { get; private init; }
    public string? Code { get; private init; }
    public string? Message { get; private init; }
    public T? Data { get; private init; }

    public static AuthServiceResult<T> Ok(T data)
    {
        return new AuthServiceResult<T> { Success = true, Data = data };
    }

    public static AuthServiceResult<T> Fail(string code, string message)
    {
        return new AuthServiceResult<T> { Success = false, Code = code, Message = message };
    }
}
