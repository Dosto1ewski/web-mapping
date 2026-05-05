namespace Standort.Infrastructure.Configuration;

public sealed class CosmosDbOptions
{
    public string AccountEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// When set, the client uses key-based auth (Cosmos Emulator / local dev).
    /// When null/empty, DefaultAzureCredential is used (production / Managed Identity).
    /// </summary>
    public string? AccountKey { get; set; }

    public string DatabaseName { get; set; } = string.Empty;
    public string GroupsContainerName { get; set; } = "groups";
    public string InviteCodesContainerName { get; set; } = "inviteCodes";
}
