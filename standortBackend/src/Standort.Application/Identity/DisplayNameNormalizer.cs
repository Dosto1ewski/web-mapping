using System.Text;

namespace Standort.Application.Identity;

public static class DisplayNameNormalizer
{
    public static string Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return raw.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
