using System.Text.Json.Serialization;

namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class MarkerDocument
{
    public const string IdPrefix = "marker:";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = "marker";

    public string GroupId { get; set; } = string.Empty;

    public string MarkerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double Lat { get; set; }

    public double Lng { get; set; }

    public string? Color { get; set; }

    public string? Notes { get; set; }

    public string CreatedByMemberId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public static string IdFor(string markerId) => $"{IdPrefix}{markerId}";
}
