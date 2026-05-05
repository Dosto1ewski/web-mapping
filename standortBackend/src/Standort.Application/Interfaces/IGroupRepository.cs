using Standort.Domain.Entities;

namespace Standort.Application.Interfaces;

public interface IGroupRepository
{
    Task<Group?> ReadGroupAsync(string groupId, CancellationToken ct);

    Task<Member?> ReadMemberAsync(string groupId, string memberId, CancellationToken ct);

    IAsyncEnumerable<Member> QueryMembersSinceAsync(string groupId, long sinceVersion, CancellationToken ct);

    Task CreateGroupAsync(Group group, Member firstMember, CancellationToken ct);

    /// <summary>
    /// Re-reads group + member, applies <paramref name="mutator"/>, then atomically
    /// (TransactionalBatch) increments the group's version and writes the resulting member doc
    /// (Replace if it existed, Create if it didn't). The repository overrides
    /// <see cref="Member.LastUpdatedVersion"/> on the written doc with the new version.
    /// Retries on optimistic concurrency conflicts.
    /// </summary>
    Task<Member> ApplyMemberWriteAsync(
        string groupId,
        string memberId,
        Func<Group, Member?, Member> mutator,
        CancellationToken ct);
}
