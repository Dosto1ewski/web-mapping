using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Net.Http.Headers;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class DeleteMarkerFunction
{
    private readonly MarkerService _markerService;

    public DeleteMarkerFunction(MarkerService markerService)
    {
        _markerService = markerService;
    }

    [Function("DeleteMarker")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "groups/{groupId}/members/{memberId}/markers/{markerId}")]
        HttpRequest req,
        string groupId,
        string memberId,
        string markerId,
        CancellationToken ct)
    {
        var token = ExtractBearerToken(req);
        if (token is null)
            return ErrorResponses.Unauthorized("Authorization header missing or malformed.");

        try
        {
            await _markerService.DeleteMarkerAsync(groupId, memberId, token, markerId, ct);
            return new NoContentResult();
        }
        catch (MemberNotFoundException)
        {
            return ErrorResponses.NotFound("Member not found.");
        }
        catch (GroupNotFoundException)
        {
            return ErrorResponses.NotFound("Group not found.");
        }
        catch (InvalidTokenException)
        {
            return ErrorResponses.Unauthorized();
        }
        catch (MarkerNotFoundException)
        {
            return ErrorResponses.NotFound("Marker not found.");
        }
    }

    private static string? ExtractBearerToken(HttpRequest req)
    {
        if (!req.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return null;
        var raw = values.ToString();
        const string prefix = "Bearer ";
        if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var token = raw[prefix.Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
