using Microsoft.Extensions.DependencyInjection;
using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class AuthService
{
    private readonly JsonStorageService storage;
    private readonly AppDbContext? db;

    public AuthService(JsonStorageService storage, IServiceProvider services)
    {
        this.storage = storage;
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
}
