using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class FetchMarkersFunction
{
    private readonly MarkerService _markerService;

    public FetchMarkersFunction(MarkerService markerService)
    {
        _markerService = markerService;
    }

    [Function("FetchAllMarkers")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "groups/{groupId}/markers")]
        HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        try
        {
            var markers = await _markerService.FetchMarkersAsync(groupId, ct);
            return new OkObjectResult(markers);
        }
        catch (GroupNotFoundException)
        {
            return ErrorResponses.NotFound("Group not found.");
        }
    }
}
