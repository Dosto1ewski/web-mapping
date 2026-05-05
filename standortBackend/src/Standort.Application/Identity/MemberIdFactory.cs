using System.Security.Cryptography;
using System.Text;

namespace Standort.Application.Identity;

/// <summary>
/// Derives a stable, deterministic member id from (groupId, normalized display name).
/// Used to make the "newest session wins" reclaim path race-free: concurrent joins of the
/// same nickname always target the same Cosmos document, where ETag-concurrency serializes them.
/// </summary>
public static class MemberIdFactory
{
    // Crockford Base32 alphabet (excludes I, L, O, U to avoid ambiguity).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string FromGroupAndNormalizedName(string groupId, string displayNameNormalized)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        ArgumentException.ThrowIfNullOrEmpty(displayNameNormalized);

        var input = $"{groupId}|{displayNameNormalized}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Take first 10 bytes → 80 bits → 16 base32 characters of entropy.
        return EncodeBase32(hash.AsSpan(0, 10));
    }

    private static string EncodeBase32(ReadOnlySpan<byte> bytes)
    {
        // 10 bytes (80 bits) → 16 chars.
        var sb = new StringBuilder(16);
        var bitBuffer = 0;
        var bitsInBuffer = 0;
        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                var index = (bitBuffer >> bitsInBuffer) & 0x1F;
                sb.Append(Alphabet[index]);
            }
        }
        if (bitsInBuffer > 0)
        {
            var index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
            sb.Append(Alphabet[index]);
        }
        return sb.ToString();
    }
}
