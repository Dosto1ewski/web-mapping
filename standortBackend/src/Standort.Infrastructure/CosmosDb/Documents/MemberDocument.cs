using System.Text.Json.Serialization;

namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class MemberDocument
{
    public const string IdPrefix = "member:";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "member";

    public string GroupId { get; set; } = string.Empty;

    public string MemberId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DisplayNameNormalized { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset TokenIssuedAt { get; set; }

    public GeoPointDocument? CurrentLocation { get; set; }

    public List<GeoPointDocument> RecentHistory { get; set; } = new();

    public long LastUpdatedVersion { get; set; }

    // Absent in legacy docs: System.Text.Json leaves missing properties at this initializer.
    public int HistoryDurationMinutes { get; set; } = 15;

    public static string IdFor(string memberId) => $"{IdPrefix}{memberId}";
}
