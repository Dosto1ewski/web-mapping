using System.Security.Cryptography;
using Standort.Application.Interfaces;

namespace Standort.Infrastructure.Security;

public sealed class CrockfordInviteCodeGenerator : IInviteCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string GenerateInviteCode()
    {
        Span<char> chars = stackalloc char[9]; // XXXX-XXXX
        Span<byte> rnd = stackalloc byte[8];
        RandomNumberGenerator.Fill(rnd);
        for (var i = 0; i < 8; i++)
        {
            // Mod 32 across alphabet of length 32 → uniform.
            chars[i < 4 ? i : i + 1] = Alphabet[rnd[i] & 0x1F];
        }
        chars[4] = '-';
        return new string(chars);
    }
}
