using System.Text.Json.Serialization;

namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class InviteCodeDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
