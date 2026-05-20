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

        RuleFor(x => x.InviteCodeHash)
            .NotEmpty()
            .Matches(@"^[0-9a-f]{64}$")
            .WithMessage("InviteCodeHash must be a lowercase SHA-256 hex string (64 chars).");
    }
}

public sealed class JoinGroupRequestValidator : AbstractValidator<JoinGroupRequest>
{
    public JoinGroupRequestValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty()
            .Matches(@"^[0-9A-HJ-NP-TV-Z]{8}-[0-9A-HJ-NP-TV-Z]{8}$")
            .WithMessage("Invite code must match XXXXXXXX-XXXXXXXX (Crockford-Base32).");

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
