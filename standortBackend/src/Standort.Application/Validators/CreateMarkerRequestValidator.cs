using FluentValidation;
using Standort.Application.DTOs;

namespace Standort.Application.Validators;

public sealed class CreateMarkerRequestValidator : AbstractValidator<CreateMarkerRequest>
{
    public CreateMarkerRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Lat).InclusiveBetween(-90, 90);
        RuleFor(r => r.Lng).InclusiveBetween(-180, 180);
        RuleFor(r => r.Color).MaximumLength(50).When(r => r.Color is not null);
        RuleFor(r => r.Notes).MaximumLength(500).When(r => r.Notes is not null);
    }
}
