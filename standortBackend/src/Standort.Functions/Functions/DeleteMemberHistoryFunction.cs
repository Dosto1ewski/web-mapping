using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class DeleteMemberHistoryFunction
{
    private readonly LocationService _locationService;

    public DeleteMemberHistoryFunction(LocationService locationService)
    {
        _locationService = locationService;
    }

    [Function("DeleteMemberHistory")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "groups/{groupId}/members/{memberId}/history")]
        HttpRequest req,
        string groupId,
        string memberId,
        CancellationToken ct)
    {
        var token = BearerToken.Extract(req);
        if (token is null)
        {
            return ErrorResponses.Unauthorized("Authorization header missing or malformed.");
        }

        try
        {
            await _locationService.DeleteHistoryAsync(groupId, memberId, token, ct);
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
        catch (ConcurrencyException ex)
        {
            return ErrorResponses.Conflict(ex.Message);
        }
    }
}
