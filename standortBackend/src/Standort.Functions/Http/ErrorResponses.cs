using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Standort.Functions.Http;

public static class ErrorResponses
{
    public sealed record ProblemBody(string Error, string Message, IReadOnlyList<FieldError>? Fields = null);

    public sealed record FieldError(string Field, string Message);

    public static IActionResult ValidationFailed(ValidationResult result)
    {
        var fields = result.Errors
            .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
            .ToList();
        return new BadRequestObjectResult(new ProblemBody("validation_failed", "One or more fields are invalid.", fields));
    }

    public static IActionResult BadRequest(string message)
        => new BadRequestObjectResult(new ProblemBody("bad_request", message));

    public static IActionResult Unauthorized(string message = "Invalid or missing token.")
        => new ObjectResult(new ProblemBody("unauthorized", message)) { StatusCode = StatusCodes.Status401Unauthorized };

    public static IActionResult NotFound(string message)
        => new NotFoundObjectResult(new ProblemBody("not_found", message));

    public static IActionResult Conflict(string message)
        => new ConflictObjectResult(new ProblemBody("conflict", message));
}
