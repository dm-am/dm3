using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Validator for ban creation
/// </summary>
internal class CreateBanValidator : AbstractValidator<CreateBan>
{
    public CreateBanValidator()
    {
        RuleFor(b => b.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(b => b.Comment)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);

        // Exactly one of the two scopes. The service coerces anything else to
        // FullBan and is right to - a malformed request must not produce a weaker
        // ban than the safe default - but as the answer to a request it is wrong: a
        // moderator who sent 0, or a client that spelled the flags as a pair, got
        // the strictest ban there is, including refused authentication, and was
        // told he got what he asked for.
        RuleFor(b => b.AccessRestrictionPolicy)
            .Must(policy => policy is AccessPolicy.DemocraticBan or AccessPolicy.FullBan)
            .WithMessage(ValidationError.Invalid);

        RuleFor(b => b.DurationHours)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(b => b.DurationHours.HasValue);

        RuleFor(b => b.ExpiresUtc)
            .GreaterThan(DateTimeOffset.UtcNow).WithMessage(ValidationError.Invalid)
            .When(b => b.ExpiresUtc.HasValue);

        RuleFor(b => b)
            .Must(b => b.DurationHours.HasValue || b.ExpiresUtc.HasValue || b.IsVoluntary)
            .WithMessage("Either DurationHours or ExpiresUtc must be specified for non-voluntary bans")
            .WithName("Duration");
    }
}
