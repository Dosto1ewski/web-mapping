using FluentValidation;
using Standort.Application.DTOs;

namespace Standort.Application.Validators;

public sealed class CreateMarkerRequestValidator : AbstractValidator<CreateMarkerRequest>
{
    public CreateMarkerRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.EncryptedLocation).NotEmpty().MaximumLength(512);
        RuleFor(r => r.EncryptedNotes).MaximumLength(1024).When(r => r.EncryptedNotes is not null);
        RuleFor(r => r.Color).MaximumLength(50).When(r => r.Color is not null);
        RuleFor(r => r.Icon).Must(i => i is null || i == "default" || i == "tree" || i == "book" || i == "champagne")
            .WithMessage("Invalid icon type");
    }
}
