using System.Security.Cryptography;
using System.Text;

namespace Sps.Apim.SelfServe.Infrastructure.AccountManagement;

public static class SecureTokenGenerator
{
    public static string Generate()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(32);

        return Base64UrlEncode(bytes);
    }

    public static string Hash(
        string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            token);

        var bytes =
            Encoding.UTF8.GetBytes(token);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(
        byte[] bytes)
    {
        return Convert
            .ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}