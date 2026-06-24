using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Application.Services;

public sealed class AuthService
{
    private readonly JsonStorageService storage;
    private readonly IEmailSender emailSender;
    private readonly AppDbContext? db;

    public AuthService(JsonStorageService storage, IEmailSender emailSender, IServiceProvider services)
    {
        this.storage = storage;
        this.emailSender = emailSender;
        db = services.GetService<AppDbContext>();
    }

    public User DemoLogin(string nickname)
    {
        var cleanNickname = string.IsNullOrWhiteSpace(nickname) ? "同頻使用者" : nickname.Trim();
        var user = new User
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = cleanNickname,
            Bio = "先用 demo 身分體驗同頻 Today"
        };

        if (db != null)
        {
            db.Users.Add(user.ToRecord());
            db.SaveChanges();
            return user;
        }

        return storage.InsertOne("users", user);
    }

    public async Task<AuthServiceResult<RegistrationStartResponse>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法建立正式帳號。");
        }

        var validation = ValidateRegister(command);
        if (validation != null)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail(validation.Value.Code, validation.Value.Message);
        }

        if (!command.TermsAccepted)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("TERMS_NOT_ACCEPTED", "請先閱讀並同意服務條款與隱私權政策。");
        }

        var email = NormalizeEmail(command.Email);
        var exists = await db.AuthAccounts.AnyAsync(x => x.Username == email, cancellationToken);
        if (exists)
        {
            return AuthServiceResult<RegistrationStartResponse>.Fail("ACCOUNT_EXISTS", "這個 Email 已經註冊過，請直接登入或使用忘記密碼。");
        }

        var code = AuthSecurity.CreateVerificationCode();
        var now = DateTimeOffset.UtcNow;
        var token = new RegistrationVerificationTokenRecord
        {
            Id = $"reg_{Guid.NewGuid():N}",
            Email = email,
            Nickname = command.Nickname.Trim(),
            PasswordHash = AuthSecurity.HashPassword(command.Password),
            BirthYear = NormalizeBirthYear(command.BirthYear),
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
            return AuthServiceResult<RegistrationStartResponse>.Fail("SMTP_NOT_CONFIGURED", "尚未設定寄信服務，無法寄出建立帳號驗證碼。");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AuthServiceResult<RegistrationStartResponse>.Fail("EMAIL_SEND_FAILED", "驗證碼寄送失敗，請稍後再試或檢查寄信設定。");
        }

        return AuthServiceResult<RegistrationStartResponse>.Ok(new RegistrationStartResponse(email, 10));
    }

    public async Task<AuthServiceResult<AuthUserResponse>> ConfirmRegistrationAsync(ConfirmRegistrationCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法建立正式帳號。");
        }

        var email = NormalizeEmail(command.Email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(command.Code))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CODE", "請輸入正確的 Email 與驗證碼。");
        }

        var exists = await db.AuthAccounts.AnyAsync(x => x.Username == email, cancellationToken);
        if (exists)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_EXISTS", "這個 Email 已經註冊過，請直接登入或使用忘記密碼。");
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
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CODE", "驗證碼錯誤或已過期。");
        }

        var user = new UserRecord
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = token.Nickname,
            AgeRange = token.BirthYear,
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

    public async Task<AuthServiceResult<AuthUserResponse>> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法登入正式帳號。");
        }

        var email = NormalizeEmail(command.Email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_LOGIN", "請輸入正確的 Email 與密碼。");
        }

        AuthAccountRecord? account;
        try
        {
            account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試。");
        }

        // 去帳號枚舉：帳號不存在與密碼錯誤回覆一致，避免探測 Email 是否註冊
        if (account == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CREDENTIALS", "Email 或密碼錯誤，請重新確認。");
        }

        if (account.IsDisabled)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_DISABLED", "此帳號目前無法登入，請聯絡客服或稍後再試。");
        }

        if (!AuthSecurity.VerifyPassword(command.Password, account.PasswordHash))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_CREDENTIALS", "Email 或密碼錯誤，請重新確認。");
        }

        UserRecord user;
        try
        {
            user = await db.Users.FirstAsync(x => x.Id == account.UserId, cancellationToken);
            account.LastLoginAt = DateTimeOffset.UtcNow;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試。");
        }

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, account.Username));
    }

    // Google 登入：用 Google 已驗證的 email 找帳號，沒有就建一個（無密碼，不能用密碼登入）。
    public async Task<AuthServiceResult<AuthUserResponse>> GoogleLoginAsync(GoogleLoginCommand command, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法登入。");
        }

        var email = NormalizeEmail(command.Email);
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<AuthUserResponse>.Fail("INVALID_GOOGLE_ACCOUNT", "無法取得有效的 Google 帳號 Email。");
        }

        var now = DateTimeOffset.UtcNow;

        AuthAccountRecord? account;
        try
        {
            account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試。");
        }

        // 既有帳號（含原本用 Email 密碼註冊的同 email）→ 直接登入
        if (account != null)
        {
            if (account.IsDisabled)
            {
                return AuthServiceResult<AuthUserResponse>.Fail("ACCOUNT_DISABLED", "此帳號目前無法登入，請聯絡客服或稍後再試。");
            }

            try
            {
                var existingUser = await db.Users.FirstAsync(x => x.Id == account.UserId, cancellationToken);
                account.LastLoginAt = now;
                account.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(existingUser.Id, existingUser.Nickname, account.Username));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試。");
            }
        }

        // 首次用 Google 登入 → 建立新帳號（密碼設成不可用的隨機 hash）
        var nickname = string.IsNullOrWhiteSpace(command.Name) ? "同頻使用者" : command.Name.Trim();
        if (nickname.Length > 20)
        {
            nickname = nickname[..20];
        }

        var user = new UserRecord
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = nickname,
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
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthServiceResult<AuthUserResponse>.Fail("DATABASE_UNAVAILABLE", "目前無法連線資料庫，請稍後再試。");
        }

        return AuthServiceResult<AuthUserResponse>.Ok(new AuthUserResponse(user.Id, user.Nickname, newAccount.Username));
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
            }
            db.SaveChanges();
            return true;
        }

        var existing = storage.ReadCollection<User>("users").FirstOrDefault(x => x.Id == userId);
        if (existing == null) return false;
        storage.UpdateOne<User>("users", userId, u =>
        {
            u.Nickname = "已離開的使用者";
            u.Bio = "";
            u.PhotoDataUrls = [];
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

    public async Task<AuthServiceResult<PasswordResetRequestResponse>> RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        if (db == null)
        {
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法重設密碼。");
        }

        email = NormalizeEmail(email);
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("INVALID_EMAIL", "請輸入正確的 Email。");
        }

        var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        if (account == null || account.IsDisabled)
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
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("SMTP_NOT_CONFIGURED", "尚未設定 SMTP，無法寄出驗證碼。請先設定寄信帳號。");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AuthServiceResult<PasswordResetRequestResponse>.Fail("EMAIL_SEND_FAILED", "驗證碼寄送失敗，請稍後再試或檢查 SMTP 設定。");
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
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("DATABASE_NOT_CONFIGURED", "目前尚未設定資料庫連線，無法重設密碼。");
        }

        email = NormalizeEmail(email);
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(code))
        {
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "請輸入正確的 Email 與驗證碼。");
        }

        var account = await db.AuthAccounts.FirstOrDefaultAsync(x => x.Username == email, cancellationToken);
        if (account == null || account.IsDisabled)
        {
            // 去帳號枚舉：與「驗證碼錯誤」回覆一致
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "驗證碼錯誤或已過期。");
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
            return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Fail("INVALID_CODE", "驗證碼錯誤或已過期。");
        }

        var user = await db.Users.FirstAsync(x => x.Id == account.UserId, cancellationToken);
        return AuthServiceResult<(AuthAccountRecord, UserRecord, PasswordResetTokenRecord)>.Ok((account, user, token));
    }

    private static (string Code, string Message)? ValidateRegister(RegisterCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Nickname) || command.Nickname.Trim().Length is < 2 or > 20)
        {
            return ("INVALID_NICKNAME", "暱稱需為 2 到 20 個字。");
        }

        if (!IsValidEmail(NormalizeEmail(command.Email)))
        {
            return ("INVALID_EMAIL", "請輸入可以收信的 Email。");
        }

        if (!IsAdultBirthYear(command.BirthYear))
        {
            return ("UNDERAGE", "未滿 18 歲暫時不能使用同頻 Today。");
        }

        return ValidatePassword(command.Password, command.ConfirmPassword);
    }

    private static (string Code, string Message)? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 64)
        {
            return ("INVALID_PASSWORD", "密碼需為 8 到 64 碼。");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return ("WEAK_PASSWORD", "密碼需同時包含英文與數字。");
        }

        if (password != confirmPassword)
        {
            return ("PASSWORD_NOT_MATCH", "兩次輸入的密碼不一致。");
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
    bool TermsAccepted);

public sealed record LoginCommand(string Email, string Password);

public sealed record GoogleLoginCommand(string Email, string Name);

public sealed record ConfirmRegistrationCommand(string Email, string Code);

public sealed record ConfirmPasswordResetCommand(
    string Email,
    string Code,
    string NewPassword,
    string ConfirmPassword);

public sealed record AuthUserResponse(string UserId, string Nickname, string Email, string? Token = null);

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
