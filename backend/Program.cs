using System.Text.Json.Serialization;

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

builder.Services.AddSingleton<SameDaySocialApp.Infrastructure.Persistence.JsonStorageService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.AuthService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayAnalyzerService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TodayEntryService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.MatchService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ModerationService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.RantService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.TaskService>();
builder.Services.AddScoped<SameDaySocialApp.Application.Services.ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseAuthorization();

app.MapControllers();

app.Run();
