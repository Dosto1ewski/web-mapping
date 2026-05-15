using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.DTOs;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class UpdateMemberSettingsFunction
{
    private readonly LocationService _locationService;
    private readonly IValidator<UpdateMemberSettingsRequest> _validator;

    public UpdateMemberSettingsFunction(
        LocationService locationService,
        IValidator<UpdateMemberSettingsRequest> validator)
    {
        _locationService = locationService;
        _validator = validator;
    }

    [Function("UpdateMemberSettings")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "groups/{groupId}/members/{memberId}/settings")]
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

        UpdateMemberSettingsRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<UpdateMemberSettingsRequest>(ct);
        }
        catch (Exception ex)
        {
            return ErrorResponses.BadRequest($"Malformed JSON body: {ex.Message}");
        }
        if (body is null)
        {
            return ErrorResponses.BadRequest("Request body is required.");
        }

        var validation = await _validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
        {
            return ErrorResponses.ValidationFailed(validation);
        }

        try
        {
            await _locationService.UpdateMemberSettingsAsync(groupId, memberId, token, body, ct);
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
