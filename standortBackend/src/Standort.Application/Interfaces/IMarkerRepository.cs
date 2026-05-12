using Standort.Domain.Entities;

namespace Standort.Application.Interfaces;

public interface IMarkerRepository
{
    Task<Marker> CreateMarkerAsync(Marker marker, CancellationToken ct);
    IAsyncEnumerable<Marker> GetMarkersAsync(string groupId, CancellationToken ct);
    Task<bool> DeleteMarkerAsync(string groupId, string markerId, CancellationToken ct);
}
