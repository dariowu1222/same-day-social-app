using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Dto;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Hubs;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Media;
using SameDaySocialApp.Infrastructure.Observability;
using SameDaySocialApp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 觀測性：Serilog 結構化日誌 + OpenTelemetry 追蹤/指標（OTLP 匯出，後端可換）
builder.AddAppObservability();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// builder.Services.AddOpenApi(); // requires .NET 9+
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
// 媒體物件儲存（provider 無關；目前 adapter 為 Supabase Storage）
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("Media"));

// 推播：憑證與排程密鑰都從環境變數進來（Render 的 Push__XXX）
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
// 排程維護端點（停權到期自動恢復）的密鑰
builder.Services.Configure<SameDaySocialApp.Controllers.MaintenanceOptions>(
    builder.Configuration.GetSection("Maintenance"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IMediaStorage, SupabaseMediaStorage>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173", "https://localhost:5173",
                "http://localhost:5174", "https://localhost:5174",
                "http://10.0.2.2:5173", "https://10.0.2.2:5173",
                // Capacitor 實機 app 的 WebView origin（Android / iOS）
                "https://localhost", "http://localhost", "capacitor://localhost"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            // 每個請求都帶 Authorization，屬於非簡單請求，瀏覽器會先送一發 OPTIONS。
            // 不設這個的話 Chromium 只快取預檢結果 5 秒、而且以 URL 為單位，
            // 一個畫面幾十個不同 URL 就是幾十發額外往返，實際 HTTP 往返數直接翻倍。
            .SetPreflightMaxAge(TimeSpan.FromHours(24));
    });
});

var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// 正式環境必須有 DB；不允許靜默退回 JSON 檔案儲存（無鎖、不跨節點、redeploy 會遺失）
if (string.IsNullOrWhiteSpace(databaseConnectionString) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "正式環境必須設定 ConnectionStrings:DefaultConnection，不允許退回 JSON 檔案儲存。");
}
if (!string.IsNullOrWhiteSpace(databaseConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(databaseConnectionString, npgsqlOptions =>
        {
            // 自動重試 transient 錯誤（連線中斷、timeout 等）
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null
            );
            // 指令 timeout 30 秒
            npgsqlOptions.CommandTimeout(30);
        });
    });
}

builder.Services.AddSingleton<JwtService>();
builder.Services.AddSignalR();

var jwtService = new JwtService(builder.Configuration, builder.Environment);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwtService.ValidationParameters;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<JsonStorageService>();
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.GoogleAuthService>();
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.FacebookAuthService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.AuthService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayAnalyzerService>();

// 今日的 AI 分析。沒設 Ai:ApiKey 就整條停用，TodayEntryService 會照常走關鍵字分析器。
builder.Services.Configure<SameDaySocialApp.Application.Services.AiAnalyzerOptions>(
    builder.Configuration.GetSection("Ai"));
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.AiTodayAnalyzer>();
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.TodayAiEnrichmentQueue>();
builder.Services.AddHostedService<SameDaySocialApp.Application.Services.TodayAiEnrichmentWorker>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayEntryService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.MatchService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ModerationService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.RantService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TaskService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ChatService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.NotificationService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ReportService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.AdminUserService>();
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.AdminAccessService>();
// 帳號狀態的即時判斷（封鎖／停權要能當場生效，而不是等 token 過期）
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.AccountStateService>();
// 後台登入：信箱驗證碼 + 可撤銷的 session（見 AdminSessionService 的說明）
builder.Services.AddScoped<SameDaySocialApp.Application.Services.AdminSessionService>();
builder.Services.AddScoped<SameDaySocialApp.Infrastructure.Security.AdminSessionFilter>();
// FirebaseApp 是行程層級單例，PushSender 跟著做成 Singleton
builder.Services.AddSingleton<SameDaySocialApp.Application.Services.PushSender>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.PushService>();

// 速率限制（防暴力登入 / 帳號枚舉 / 互動刷量）；分區鍵：登入者用 userId，否則用來源 IP
static string RateKey(HttpContext ctx) =>
    ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
    ?? ctx.Connection.RemoteIpAddress?.ToString()
    ?? "anon";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 認證類：每 IP 每分鐘 10 次
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        RateKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // 互動寫入類：每使用者每分鐘 60 次
    options.AddPolicy("write", http => RateLimitPartition.GetFixedWindowLimiter(
        RateKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail("RATE_LIMITED", "操作太頻繁，請稍後再試。"), ct);
    };
});

var app = builder.Build();

// 每個請求記結構化計時日誌（耗時 ms、狀態碼、UserId、TraceId）→ 送 OTLP
app.UseAppObservability();

// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi(); // requires .NET 9+
// }

// Render 把服務放在反向代理後面，真實來源 IP 在 X-Forwarded-For。
// 不處理的話 RemoteIpAddress 對所有外部請求都是同一個代理位址，於是：
//   1. 速率限制的分區鍵對匿名端點變成全站共用一個桶——任何人用一行 curl
//      每分鐘打 10 次登入，就能讓所有人的登入／註冊／忘記密碼一起 429。
//   2. admin_sessions.created_ip 永遠記到代理 IP，事後查不到來源。
// 必須在速率限制與任何讀 IP 的東西之前。
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
// 預設只信任 loopback，在容器裡等於整個中介軟體不作用。
// 容器只接受來自平台代理的流量，所以這裡清空限制。
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// 安全標頭。擺在最前面，連錯誤回應與靜態檔案都要帶上。
//
// 後台頁面看得到所有人的私訊，所以 frame-ancestors 一定要有：沒有它，
// 別人可以把後台嵌進自己的頁面、疊一層透明按鈕騙管理者點下去（點擊劫持）。
// X-Frame-Options 是給還不認得 CSP 的舊瀏覽器的同一道防線。
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "img-src 'self' data: https:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    // 後台的回應一律不給快取：共用電腦上按上一頁不該還看得到別人的私訊
    if (context.Request.Path.StartsWithSegments("/api/admin")
        || context.Request.Path.StartsWithSegments("/api/moderation")
        || context.Request.Path.StartsWithSegments("/admin"))
    {
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        headers["Pragma"] = "no-cache";
    }

    await next();
});

if (!app.Environment.IsDevelopment())
{
    // 本機是 http，開了會讓瀏覽器把 localhost 記成只能走 https
    app.UseHsts();
}

// 審核後台的靜態檔案。由 Dockerfile 的 node 階段建進 wwwroot/admin，
// 本機開發時這個資料夾不存在，後台走它自己的 dev server（:5174）。
app.UseStaticFiles();

app.UseCors("Frontend");
app.UseAuthentication();

// 帳號被封鎖／停權／註銷之後，手上那顆還沒過期的 token 必須當場失效。
//
// 原本的處置只把 auth_accounts.is_disabled 設成 true，而那個欄位只有登入路徑
// 會讀——所以被封鎖的人可以繼續用舊 token 傳訊息、讀對話，最長七天。
// 「永久封鎖」對正在騷擾人的帳號形同虛設，而那正是最需要它生效的場合。
//
// 狀態有 60 秒的記憶體快取（見 AccountStateService），但後台處置時會主動清掉，
// 所以從後台按下去是當場生效，不是等一分鐘。
app.Use(async (context, next) =>
{
    var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                 ?? context.User?.FindFirst("sub")?.Value;

    if (!string.IsNullOrWhiteSpace(userId))
    {
        var accountState = context.RequestServices
            .GetRequiredService<SameDaySocialApp.Application.Services.AccountStateService>();

        if (!await accountState.IsActiveAsync(userId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                SameDaySocialApp.Application.Dto.ApiResponse<object>.Fail(
                    "ACCOUNT_DISABLED", "這個帳號目前無法使用。"));
            return;
        }
    }

    await next();
});

// 後台 token 只能用在後台。它和 App token 是同一個 issuer/audience，
// 所以撤銷 session 之後那顆 token 對一般端點仍然驗得過——除非在這裡擋掉。
app.Use(async (context, next) =>
{
    var isAdminToken = context.User?.FindFirst(SameDaySocialApp.Application.Services.JwtService.AdminScopeClaim)?.Value
                       == SameDaySocialApp.Application.Services.JwtService.AdminScopeValue;
    var isAdminPath = context.Request.Path.StartsWithSegments("/api/admin")
                      || context.Request.Path.StartsWithSegments("/api/moderation");

    if (isAdminToken && !isAdminPath)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(
            SameDaySocialApp.Application.Dto.ApiResponse<object>.Fail(
                "ADMIN_TOKEN_SCOPE", "後台憑證不能用於一般功能。"));
        return;
    }

    await next();
});

app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// 健康檢查：託管平台（Render / Railway / Cloud Run）用來判斷服務是否存活。
// 刻意不碰資料庫，避免 DB 短暫不可用時整個服務被判定失敗而重啟。
// 白名單沒設 = 後台對所有人回 404。那個行為是對的（預設拒絕），
// 但從外面看起來像「後台壞了」而不是「設定漏了」，所以啟動時先喊一聲。
if (!app.Environment.IsDevelopment())
{
    var adminAccess = app.Services.GetRequiredService<SameDaySocialApp.Application.Services.AdminAccessService>();
    if (!adminAccess.IsConfigured)
    {
        app.Logger.LogWarning(
            "Moderation__AdminUserIds 未設定：審核後台對所有人回 404，沒有任何帳號進得去。");
    }
}

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

// 後台是用 BrowserRouter 的 SPA：/admin/users/xxx 這種深層路徑要交回 index.html，
// 否則重新整理會變成 404。
app.MapFallbackToFile("/admin/{*path}", "admin/index.html");
app.MapHub<ChatHub>("/hubs/chat", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
});
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="zh-Hant">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>同頻 Today</title>
      <style>
        body {
          margin: 0;
          min-height: 100vh;
          display: grid;
          place-items: center;
          background: #fff8ef;
          color: #4a260f;
          font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
        }
        main {
          width: min(90vw, 420px);
          text-align: center;
          line-height: 1.7;
        }
        h1 { margin-bottom: 8px; }
        a {
          display: inline-block;
          margin-top: 16px;
          border-radius: 14px;
          padding: 12px 20px;
          background: #d86c4e;
          color: #fff8f1;
          text-decoration: none;
          font-weight: 800;
        }
      </style>
      <script>
        const target = 'http://localhost:5173';
        async function waitForFrontend() {
          try {
            await fetch(target, { mode: 'no-cors' });
            window.location.replace(target);
          } catch {
            setTimeout(waitForFrontend, 1000);
          }
        }
        waitForFrontend();
      </script>
    </head>
    <body>
      <main>
        <h1>同頻 Today 正在啟動</h1>
        <p>後端已啟動，正在等待前端開發伺服器。如果畫面沒有自動跳轉，請確認 Visual Studio 輸出視窗是否有 npm / Vite 錯誤。</p>
        <a href="http://localhost:5173">前往前端</a>
      </main>
    </body>
    </html>
    """,
    "text/html"));
}
else
{
    // 正式環境：根路徑只回一句話，不暴露內部資訊、也不轉址到本機前端。
    app.MapGet("/", () => Results.Text("同頻 Today API"));
}

app.Run();
