using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SameDaySocialApp.Application.Services;
using SameDaySocialApp.Domain.Enums;
using SameDaySocialApp.Infrastructure.Persistence;
using SameDaySocialApp.Infrastructure.Persistence.Models;

namespace SameDaySocialApp.Tests;

/// <summary>
/// 帳號狀態那幾組測試共用的腳手架。
///
/// 每個測試各自一份 InMemory 資料庫（以測試名稱當資料庫名），彼此不互相汙染。
/// </summary>
internal static class TestWorld
{
    public static AppDbContext Db(string databaseName)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    /// <summary>
    /// 停權刻意讓 users.status 停在 SUSPENDED——真實資料就長這樣，因為把它寫回
    /// ACTIVE 的 AdminUserService.RestoreExpiredSuspensions 不是期限一到就當場跑的。
    /// </summary>
    public static UserRecord User(string id, AccountStatus status = AccountStatus.ACTIVE, DateTimeOffset? suspendedUntil = null)
        => new()
        {
            Id = id,
            Nickname = id,
            Status = status.ToString(),
            SuspendedUntil = suspendedUntil
        };

    public static DateTimeOffset StillSuspended => DateTimeOffset.UtcNow.AddDays(3);

    public static DateTimeOffset SuspensionOver => DateTimeOffset.UtcNow.AddMinutes(-1);

    /// <summary>
    /// 某人的登入帳號。
    ///
    /// isDisabled 預設跟著處置當下的狀態走，因為真實資料就是這樣寫下的
    /// （AdminUserService.ChangeStatus 兩個欄位一起寫）。停權到期的那一刻只有
    /// suspended_until 變得有意義，is_disabled 仍然留在 true——登入會不會放行，
    /// 考的就是這個落差。
    /// </summary>
    public static AuthAccountRecord Account(UserRecord user, string password)
        => new()
        {
            Id = $"auth_{user.Id}",
            UserId = user.Id,
            Username = $"{user.Id}@example.com",
            PasswordHash = AuthSecurity.HashPassword(password),
            IsDisabled = user.Status != AccountStatus.ACTIVE.ToString()
        };

    /// <summary>
    /// JsonStorageService 的建構子要 IWebHostEnvironment，而且會建 Data 目錄。
    /// 資料庫路徑下它一行都不會被呼叫，但還是指到各自的暫存資料夾，不碰專案目錄。
    /// </summary>
    public static JsonStorageService Storage() => new(new TestEnvironment());

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "backend.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "tongpin-tests", Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
