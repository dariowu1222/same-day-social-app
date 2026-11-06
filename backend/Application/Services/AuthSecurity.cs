using System.Security.Cryptography;
using System.Text;

namespace SameDaySocialApp.Application.Services;

public static class AuthSecurity
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 120_000;

    /// <summary>
    /// 一組驗證碼可以被猜錯幾次。
    ///
    /// 碼是 6 位數、有效 10 分鐘，而唯一的防線原本是每 IP 每分鐘 10 次。
    /// 一個 IP 猜不完一百萬組，但一千個 IP 在十分鐘內可以猜掉四十萬組左右——
    /// 也就是大約四成的機率直接重設掉別人的密碼。單一驗證碼自己要會數。
    ///
    /// 五次對真的打錯字的人夠用（6 位數，手輸很難連錯五次），對猜的人則是把
    /// 每組碼的期望值從四十萬壓回五。
    /// </summary>
    public const int MaxCodeAttempts = 5;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static string CreateVerificationCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    public static string HashVerificationCode(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = SHA256.HashData(Combine(salt, Encoding.UTF8.GetBytes(code.Trim())));
        return $"sha256${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyVerificationCode(string code, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 3 || parts[0] != "sha256")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = SHA256.HashData(Combine(salt, Encoding.UTF8.GetBytes(code.Trim())));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
}
