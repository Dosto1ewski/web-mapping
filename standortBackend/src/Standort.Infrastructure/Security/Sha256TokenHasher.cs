using System.Security.Cryptography;
using System.Text;
using Standort.Application.Interfaces;

namespace Standort.Infrastructure.Security;

public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string plainToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainToken);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToBase64String(bytes);
    }

    public bool Verify(string plainToken, string storedHash)
    {
        if (string.IsNullOrEmpty(plainToken) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }
        var computed = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        Span<byte> stored = stackalloc byte[computed.Length];
        if (!Convert.TryFromBase64String(storedHash, stored, out var written) || written != computed.Length)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
