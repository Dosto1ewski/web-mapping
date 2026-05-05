using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class GetGroupLocationsFunction
{
    private readonly LocationService _locationService;

    public GetGroupLocationsFunction(LocationService locationService)
    {
        _locationService = locationService;
    }

    [Function("GetGroupLocations")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "groups/{groupId}/locations")]
        HttpRequest req,
        string groupId,
        CancellationToken ct)
    {
        long? sinceVersion = null;
        if (req.Query.TryGetValue("sinceVersion", out var raw))
        {
            if (!long.TryParse(raw.ToString(), out var parsed) || parsed < 0)
            {
                return ErrorResponses.BadRequest("sinceVersion must be a non-negative integer.");
            }
            sinceVersion = parsed;
        }

        try
        {
            var response = await _locationService.GetLocationsAsync(groupId, sinceVersion, ct);
            if (response is null)
            {
                return new StatusCodeResult(StatusCodes.Status304NotModified);
            }
            return new OkObjectResult(response);
        }
        catch (GroupNotFoundException)
        {
            return ErrorResponses.NotFound("Group not found.");
        }
    }
}
