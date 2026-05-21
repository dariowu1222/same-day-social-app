using SameDaySocialApp.Domain.Entities;
using SameDaySocialApp.Infrastructure.Persistence;

namespace SameDaySocialApp.Application.Services;

public sealed class AuthService(JsonStorageService storage)
{
    public User DemoLogin(string nickname)
    {
        var cleanNickname = string.IsNullOrWhiteSpace(nickname) ? "今天的旅人" : nickname.Trim();
        var user = new User
        {
            Id = $"user_{Guid.NewGuid():N}",
            Nickname = cleanNickname,
            Bio = "慢慢認識就好。"
        };

        return storage.InsertOne("users", user);
    }
}
