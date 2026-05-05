using FluentValidation;
using Standort.Application.DTOs;

namespace Standort.Application.Validators;

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(1, 80);

        RuleFor(x => x.CreatedByDisplayName)
            .SetValidator(new DisplayNameValidator());
    }
}

public sealed class JoinGroupRequestValidator : AbstractValidator<JoinGroupRequest>
{
    public JoinGroupRequestValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty()
            .Matches(@"^[0-9A-HJ-NP-TV-Z]{4}-[0-9A-HJ-NP-TV-Z]{4}$")
            .WithMessage("Invite code must match XXXX-XXXX (Crockford-Base32).");

        RuleFor(x => x.DisplayName)
            .SetValidator(new DisplayNameValidator());
    }
}

public sealed class DisplayNameValidator : AbstractValidator<string>
{
    public DisplayNameValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .Must(s => s.Trim().Length is >= 1 and <= 32)
                .WithMessage("Display name must be 1-32 characters after trimming.")
            .Matches(@"^[\p{L}\p{N} _.\-]+$")
                .WithMessage("Display name may contain letters, digits, spaces, '_', '.', '-'.");
    }
}
