using System.Text.Json.Serialization;

namespace Standort.Infrastructure.CosmosDb.Documents;

public sealed class GroupDocument
{
    public const string GroupDocId = "group";

    // Cosmos requires the id property to be lower-case "id".
    [JsonPropertyName("id")]
    public string Id { get; set; } = GroupDocId;

    public string Type { get; set; } = "group";

    public string GroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public long Version { get; set; }
}
