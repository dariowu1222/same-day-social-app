using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
    /// ACTIVE 的 AdminUserService.RestoreExpiredSuspensions 要等有人開後台才會跑。
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
