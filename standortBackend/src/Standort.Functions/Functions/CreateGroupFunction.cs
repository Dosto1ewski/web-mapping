using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Standort.Application.DTOs;
using Standort.Application.Services;
using Standort.Functions.Http;

namespace Standort.Functions.Functions;

public sealed class CreateGroupFunction
{
    private readonly GroupService _groupService;
    private readonly IValidator<CreateGroupRequest> _validator;

    public CreateGroupFunction(GroupService groupService, IValidator<CreateGroupRequest> validator)
    {
        _groupService = groupService;
        _validator = validator;
    }

    [Function("CreateGroup")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "groups")] HttpRequest req,
        CancellationToken ct)
    {
        CreateGroupRequest? body;
        try
        {
            body = await req.ReadFromJsonAsync<CreateGroupRequest>(ct);
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

        var response = await _groupService.CreateGroupAsync(body, ct);
        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }
}
