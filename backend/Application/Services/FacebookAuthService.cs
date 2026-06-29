using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SameDaySocialApp.Application.Services;

// 驗證 App / Web 端傳來的 Facebook access token。
// AppId 非機密（前端也用）；AppSecret 為機密 → 放 User Secrets / env：Authentication__Facebook__AppSecret。
public sealed class FacebookAuthService
{
    private readonly string? appId;
    private readonly string? appSecret;
    private readonly IHttpClientFactory httpClientFactory;

    public FacebookAuthService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        appId = configuration["Authentication:Facebook:AppId"];
        appSecret = configuration["Authentication:Facebook:AppSecret"];
        this.httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(appSecret);

    // 成功回傳使用者基本資料；token 無效、非本 App、未授權 email、過期等都回 null。
    public async Task<FacebookUserInfo?> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || !IsConfigured)
        {
            return null;
        }

        try
        {
            var http = httpClientFactory.CreateClient();

            // 1) debug_token：確認此 token 是發給「我們的」App 且仍有效，防止他 App 的 token 冒用。
            var appToken = $"{appId}|{appSecret}";
            var debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appToken)}";
            using var debugResp = await http.GetAsync(debugUrl, cancellationToken);
            if (!debugResp.IsSuccessStatusCode)
            {
                return null;
            }
            using var debugDoc = JsonDocument.Parse(await debugResp.Content.ReadAsStringAsync(cancellationToken));
            if (!debugDoc.RootElement.TryGetProperty("data", out var data))
            {
                return null;
            }
            var isValid = data.TryGetProperty("is_valid", out var validProp) && validProp.GetBoolean();
            var tokenAppId = data.TryGetProperty("app_id", out var appIdProp) ? appIdProp.GetString() : null;
            if (!isValid || tokenAppId != appId)
            {
                return null;
            }

            // 2) /me：取 email + name（email 需使用者授權 public_profile + email）
            var meUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
            using var meResp = await http.GetAsync(meUrl, cancellationToken);
            if (!meResp.IsSuccessStatusCode)
            {
                return null;
            }
            using var meDoc = JsonDocument.Parse(await meResp.Content.ReadAsStringAsync(cancellationToken));
            var root = meDoc.RootElement;
            var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(email))
            {
                // 使用者未授權 email → 無法以 email 建立/登入帳號
                return null;
            }

            return new FacebookUserInfo(email!, name ?? "");
        }
        catch
        {
            return null;
        }
    }
}

public sealed record FacebookUserInfo(string Email, string Name);
