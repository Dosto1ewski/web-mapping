using Standort.Domain.Entities;
using Standort.Domain.ValueObjects;

namespace Standort.Infrastructure.CosmosDb.Documents;

internal static class DocumentMappers
{
    public static GeoPointDocument ToDocument(this GeoCoordinate value) => new()
    {
        Lat = value.Lat,
        Lng = value.Lng,
        AccuracyMeters = value.AccuracyMeters,
        RecordedAt = value.RecordedAt,
        ServerReceivedAt = value.ServerReceivedAt,
    };

    public static GeoCoordinate ToDomain(this GeoPointDocument doc) => new(
        Lat: doc.Lat,
        Lng: doc.Lng,
        AccuracyMeters: doc.AccuracyMeters,
        RecordedAt: doc.RecordedAt,
        ServerReceivedAt: doc.ServerReceivedAt);

    public static GroupDocument ToDocument(this Group group) => new()
    {
        Id = GroupDocument.GroupDocId,
        Type = "group",
        GroupId = group.GroupId,
        Name = group.Name,
        CreatedAt = group.CreatedAt,
        Version = group.Version,
    };

    public static Group ToDomain(this GroupDocument doc) => new()
    {
        GroupId = doc.GroupId,
        Name = doc.Name,
        CreatedAt = doc.CreatedAt,
        Version = doc.Version,
    };

    public static MemberDocument ToDocument(this Member member) => new()
    {
        Id = MemberDocument.IdFor(member.MemberId),
        Type = "member",
        GroupId = member.GroupId,
        MemberId = member.MemberId,
        DisplayName = member.DisplayName,
        DisplayNameNormalized = member.DisplayNameNormalized,
        TokenHash = member.TokenHash,
        TokenIssuedAt = member.TokenIssuedAt,
        CurrentLocation = member.CurrentLocation?.ToDocument(),
        RecentHistory = member.RecentHistory.Select(p => p.ToDocument()).ToList(),
        LastUpdatedVersion = member.LastUpdatedVersion,
        HistoryDurationMinutes = member.HistoryDurationMinutes,
    };

    public static Member ToDomain(this MemberDocument doc) => new()
    {
        MemberId = doc.MemberId,
        GroupId = doc.GroupId,
        DisplayName = doc.DisplayName,
        DisplayNameNormalized = doc.DisplayNameNormalized,
        TokenHash = doc.TokenHash,
        TokenIssuedAt = doc.TokenIssuedAt,
        CurrentLocation = doc.CurrentLocation?.ToDomain(),
        RecentHistory = doc.RecentHistory.Select(p => p.ToDomain()).ToList(),
        LastUpdatedVersion = doc.LastUpdatedVersion,
        HistoryDurationMinutes = doc.HistoryDurationMinutes,
    };

    public static MarkerDocument ToDocument(this Marker marker) => new()
    {
        Id = MarkerDocument.IdFor(marker.MarkerId),
        Type = "marker",
        GroupId = marker.GroupId,
        MarkerId = marker.MarkerId,
        Name = marker.Name,
        Lat = marker.Lat,
        Lng = marker.Lng,
        Color = marker.Color,
        Notes = marker.Notes,
        Icon = marker.Icon,
        CreatedByMemberId = marker.CreatedByMemberId,
        CreatedAt = marker.CreatedAt,
    };

    public static Marker ToDomain(this MarkerDocument doc) => new()
    {
        MarkerId = doc.MarkerId,
        GroupId = doc.GroupId,
        Name = doc.Name,
        Lat = doc.Lat,
        Lng = doc.Lng,
        Color = doc.Color,
        Notes = doc.Notes,
        Icon = doc.Icon,
        CreatedByMemberId = doc.CreatedByMemberId,
        CreatedAt = doc.CreatedAt,
    };
}
