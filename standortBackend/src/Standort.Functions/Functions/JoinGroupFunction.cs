using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.DTOs;
using Standort.Application.Services;
using Standort.Domain.DomainExceptions;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class JoinGroupFunction
{
    private readonly GroupService _groupService;
    private readonly IValidator<JoinGroupRequest> _validator;

    public JoinGroupFunction(GroupService groupService, IValidator<JoinGroupRequest> validator)
    {
        _groupService = groupService;
        _validator = validator;
    }

    [Function("JoinGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups/join")] HttpRequest req,
        CancellationToken ct)
    {
        JoinGroupRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<JoinGroupRequest>(ct);
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
            var response = await _groupService.JoinGroupAsync(body, ct);
            return new OkObjectResult(response);
        }
        catch (InviteCodeNotFoundException)
        {
            return ErrorResponses.NotFound("Invite code not found.");
        }
    }
}
