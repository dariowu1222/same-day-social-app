using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Infrastructure.Email;
using SameDaySocialApp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// builder.Services.AddOpenApi(); // requires .NET 9+
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
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
            .AllowAnyMethod();
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

var jwtService = new JwtService(builder.Configuration, builder.Environment);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwtService.ValidationParameters;
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<JsonStorageService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.AuthService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayAnalyzerService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayEntryService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.MatchService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ModerationService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.RantService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TaskService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ChatService>();

var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi(); // requires .NET 9+
// }

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
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

app.Run();
