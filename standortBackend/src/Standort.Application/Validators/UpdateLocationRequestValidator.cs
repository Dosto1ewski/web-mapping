using FluentValidation;
using Standort.Application.DTOs;
using Standort.Application.Interfaces;

namespace Standort.Application.Validators;

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public static readonly TimeSpan MaxClockSkewFuture = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan MaxRecordedAtAge = TimeSpan.FromHours(24);

    public UpdateLocationRequestValidator(ISystemClock clock)
    {
        RuleFor(x => x.EncryptedLocation)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.RecordedAt)
            .Must((req, recordedAt) =>
            {
                var now = clock.UtcNow;
                if (recordedAt > now + MaxClockSkewFuture) return false;
                if (recordedAt < now - MaxRecordedAtAge) return false;
                return true;
            })
            .WithMessage($"recordedAt must be within {MaxRecordedAtAge.TotalHours:F0}h in the past and {MaxClockSkewFuture.TotalSeconds:F0}s in the future.");
    }
}
