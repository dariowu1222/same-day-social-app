using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace SameDaySocialApp.Application.Services;

// 驗證 App / Web 端傳來的 Google ID token。
// WebClientId 由設定提供（Authentication:Google:WebClientId，可用 env Authentication__Google__WebClientId）。
// Google Web Client ID 並非機密（前端也會用到），可放 appsettings；正式部署建議用環境變數覆寫。
public sealed class GoogleAuthService
{
    private readonly string? webClientId;

    public GoogleAuthService(IConfiguration configuration)
    {
        webClientId = configuration["Authentication:Google:WebClientId"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(webClientId);

    // 驗證成功回傳使用者基本資料；失敗（過期、簽章錯、audience 不符、email 未驗證）回 null。
    public async Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || !IsConfigured)
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { webClientId! },
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Email) || !payload.EmailVerified)
            {
                return null;
            }

            return new GoogleUserInfo(payload.Email, payload.Name ?? "");
        }
        catch
        {
            // 簽章不符、token 過期、audience 不符等都會丟例外 → 視為驗證失敗
            return null;
        }
    }
}

public sealed record GoogleUserInfo(string Email, string Name);
