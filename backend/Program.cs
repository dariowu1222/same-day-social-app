using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SameDaySocialApp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(databaseConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(databaseConnectionString);
    });
}

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
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
