using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace SameDaySocialApp.Application.Services;

public sealed class JwtService
{
    private readonly string _secret;
    private const string Issuer = "same-day-social-app";
    private const string Audience = "same-day-social-app";
    private const int MinSecretBytes = 32;

    public JwtService(IConfiguration configuration, IHostEnvironment environment)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // 正式環境必須明確提供密鑰：缺少時 fail-fast，避免靜默用隨機密鑰造成
            // 「每次重啟使用者被登出」或「多台機器 token 互不相容」。
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "缺少 Jwt:Secret。正式環境請以環境變數 Jwt__Secret 提供至少 32 位元組的強密鑰。");
            }
            // 開發環境：自動產生（重啟後失效，僅供本機開發）
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }
        else if (!environment.IsDevelopment() && Encoding.UTF8.GetByteCount(secret) < MinSecretBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Secret 太短。正式環境請使用至少 {MinSecretBytes} 位元組的強密鑰。");
        }
        _secret = secret;
    }

    public string IssueToken(string userId, string nickname, TimeSpan? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("nickname", nickname),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromDays(7)),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 後台專用 token。
    ///
    /// 刻意和一般 App token 分開簽，而且帶 scope=admin：後台端點只認這個 scope，
    /// 所以手機上那顆七天有效的 App token 就算外流，也打不開後台的任何一扇門。
    ///
    /// sid 指向 admin_sessions 的一列。真正的有效期由那一列決定（可撤銷、可閒置逾時），
    /// JWT 自己的 exp 只是最後一道保險，設得比 session 的絕對上限略寬。
    /// </summary>
    public string IssueAdminToken(string userId, string nickname, string sessionId, TimeSpan expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("nickname", nickname),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(AdminScopeClaim, AdminScopeValue),
            new Claim(AdminSessionClaim, sessionId),
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public const string AdminScopeClaim = "scope";
    public const string AdminScopeValue = "admin";
    public const string AdminSessionClaim = "sid";

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
    };
}
