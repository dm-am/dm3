using System;
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
