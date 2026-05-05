namespace Standort.Application.Interfaces;

public interface IInviteCodeRepository
{
    /// <summary>
    /// Tries to claim the given invite code for the given group. Returns false if the code is already taken.
    /// </summary>
    Task<bool> TryCreateAsync(string inviteCode, string groupId, DateTimeOffset createdAt, CancellationToken ct);

    Task<string?> GetGroupIdByCodeAsync(string inviteCode, CancellationToken ct);
}
