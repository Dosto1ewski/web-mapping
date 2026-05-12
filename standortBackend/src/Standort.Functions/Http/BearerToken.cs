using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Standort.Functions.Http;

public static class BearerToken
{
    private const string Prefix = "Bearer ";

    public static string? Extract(HttpRequest req)
    {
        if (!req.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return null;
        var raw = values.ToString();
        if (!raw.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var token = raw[Prefix.Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
