using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.DTOs;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class CreateMarkerFunction
{
    private readonly MarkerService _markerService;
    private readonly IValidator<CreateMarkerRequest> _validator;

    public CreateMarkerFunction(MarkerService markerService, IValidator<CreateMarkerRequest> validator)
    {
        _markerService = markerService;
        _validator = validator;
    }

    [Function("CreateMarker")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "groups/{groupId}/members/{memberId}/markers")]
        HttpRequest req,
        string groupId,
        string memberId,
        CancellationToken ct)
    {
        var token = BearerToken.Extract(req);
        if (token is null)
            return ErrorResponses.Unauthorized("Authorization header missing or malformed.");

        CreateMarkerRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<CreateMarkerRequest>(ct);
        }
        catch (Exception ex)
        {
            return ErrorResponses.BadRequest($"Malformed JSON body: {ex.Message}");
        }
        if (body is null)
            return ErrorResponses.BadRequest("Request body is required.");

        var validation = await _validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
            return ErrorResponses.ValidationFailed(validation);

        try
        {
            var marker = await _markerService.CreateMarkerAsync(groupId, memberId, token, body, ct);
            return new ObjectResult(marker) { StatusCode = StatusCodes.Status201Created };
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
    }

}
