using Standort.Application.DTOs;
using Standort.Application.Interfaces;
using Standort.Domain.DomainExceptions;
using Standort.Domain.Entities;

namespace Standort.Application.Services;

public sealed class MarkerService
{
    private readonly IGroupRepository _groupRepo;
    private readonly IMarkerRepository _markerRepo;
    private readonly ITokenHasher _tokenHasher;
    private readonly ISystemClock _clock;

    public MarkerService(
        IGroupRepository groupRepo,
        IMarkerRepository markerRepo,
        ITokenHasher tokenHasher,
        ISystemClock clock)
    {
        _groupRepo = groupRepo;
        _markerRepo = markerRepo;
        _tokenHasher = tokenHasher;
        _clock = clock;
    }

    public async Task<MarkerDto> CreateMarkerAsync(
        string groupId,
        string memberId,
        string presentedToken,
        CreateMarkerRequest request,
        CancellationToken ct)
    {
        var member = await _groupRepo.ReadMemberAsync(groupId, memberId, ct)
            ?? throw new MemberNotFoundException(groupId, memberId);

        if (!_tokenHasher.Verify(presentedToken, member.TokenHash))
            throw new InvalidTokenException();

        var marker = new Marker
        {
            MarkerId = Guid.NewGuid().ToString("N"),
            GroupId = groupId,
            Name = request.Name,
            EncryptedLocation = request.EncryptedLocation,
            EncryptedNotes = request.EncryptedNotes,
            Color = request.Color,
            Icon = request.Icon,
            CreatedByMemberId = memberId,
            CreatedAt = _clock.UtcNow,
        };

        var created = await _markerRepo.CreateMarkerAsync(marker, ct);
        return ToDto(created);
    }

    public async Task<IReadOnlyList<MarkerDto>> FetchMarkersAsync(
        string groupId,
        CancellationToken ct)
    {
        _ = await _groupRepo.ReadGroupAsync(groupId, ct)
            ?? throw new GroupNotFoundException(groupId);

        var markers = new List<MarkerDto>();
        await foreach (var marker in _markerRepo.GetMarkersAsync(groupId, ct))
            markers.Add(ToDto(marker));

        return markers;
    }

    public async Task DeleteMarkerAsync(
        string groupId,
        string memberId,
        string presentedToken,
        string markerId,
        CancellationToken ct)
    {
        var member = await _groupRepo.ReadMemberAsync(groupId, memberId, ct)
            ?? throw new MemberNotFoundException(groupId, memberId);

        if (!_tokenHasher.Verify(presentedToken, member.TokenHash))
            throw new InvalidTokenException();

        var found = await _markerRepo.DeleteMarkerAsync(groupId, markerId, ct);
        if (!found)
            throw new MarkerNotFoundException(markerId);
    }

    private static MarkerDto ToDto(Marker marker) => new(
        MarkerId: marker.MarkerId,
        Name: marker.Name,
        EncryptedLocation: marker.EncryptedLocation,
        EncryptedNotes: marker.EncryptedNotes,
        Color: marker.Color,
        Icon: marker.Icon,
        CreatedByMemberId: marker.CreatedByMemberId,
        CreatedAt: marker.CreatedAt);
}
