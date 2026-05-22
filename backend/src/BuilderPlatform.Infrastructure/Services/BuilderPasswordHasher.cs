using System.Security.Cryptography;
using System.Text;

namespace BuilderPlatform.Infrastructure.Services;

public static class BuilderPasswordHasher
{
    private const int Iterations   = 100_000;
    private const int KeyLength    = 32;
    private const int SaltLength   = 16;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt, Iterations, HashAlgorithmName.SHA256, KeyLength);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var computed = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt, Iterations, HashAlgorithmName.SHA256, KeyLength);
            return CryptographicOperations.FixedTimeEquals(expected, computed);
        }
        catch { return false; }
    }
}
