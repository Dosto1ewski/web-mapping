using System.Security.Cryptography;
using Standort.Application.Interfaces;

namespace Standort.Infrastructure.Security;

public sealed class RandomTokenGenerator : ITokenGenerator
{
    private const int TokenByteLength = 32;

    public string GeneratePlainToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
