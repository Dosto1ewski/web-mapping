namespace Standort.Domain.DomainExceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class GroupNotFoundException : DomainException
{
    public GroupNotFoundException(string groupId)
        : base($"Group '{groupId}' was not found.") { }
}

public sealed class MemberNotFoundException : DomainException
{
    public MemberNotFoundException(string groupId, string memberId)
        : base($"Member '{memberId}' was not found in group '{groupId}'.") { }
}

public sealed class InvalidTokenException : DomainException
{
    public InvalidTokenException() : base("The provided member token is invalid.") { }
}

public sealed class InviteCodeNotFoundException : DomainException
{
    public InviteCodeNotFoundException(string inviteCode)
        : base($"Invite code '{inviteCode}' is not valid.") { }
}

public sealed class MarkerNotFoundException : DomainException
{
    public MarkerNotFoundException(string markerId)
        : base($"Marker '{markerId}' was not found.") { }
}

public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message) { }
}

public sealed class NameInUseException : DomainException
{
    public NameInUseException(string displayName, DateTimeOffset lastSeen, bool hasLocation)
        : base($"Display name '{displayName}' is already active in this group.")
    {
        DisplayName = displayName;
        LastSeen = lastSeen;
        HasLocation = hasLocation;
    }

    public string DisplayName { get; }
    public DateTimeOffset LastSeen { get; }
    public bool HasLocation { get; }
}
