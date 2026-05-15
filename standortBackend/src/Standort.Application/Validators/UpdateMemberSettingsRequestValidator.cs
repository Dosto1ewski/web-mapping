using FluentValidation;
using Standort.Application.DTOs;
using Standort.Application.Services;

namespace Standort.Application.Validators;

public sealed class UpdateMemberSettingsRequestValidator : AbstractValidator<UpdateMemberSettingsRequest>
{
    public UpdateMemberSettingsRequestValidator()
    {
        RuleFor(r => r.HistoryDurationMinutes)
            .InclusiveBetween(0, LocationService.MaxHistoryDurationMinutes)
            .WithMessage($"historyDurationMinutes must be between 0 and {LocationService.MaxHistoryDurationMinutes}.");
    }
}
