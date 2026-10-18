using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Validator for a change of warning points
/// </summary>
/// <remarks>
/// The explanation is required, unlike the reason of a lifted ban: this one is
/// shown to everybody who reads the log, and a correction with no words under it
/// leaves the original verdict standing beside points that contradict it.
/// </remarks>
internal class ChangeWarningPointsValidator : AbstractValidator<ChangeWarningPoints>
{
    public ChangeWarningPointsValidator()
    {
        RuleFor(c => c.Points)
            .NotNull().WithMessage(ValidationError.Empty)
            .InclusiveBetween(0, 6).WithMessage(ValidationError.Invalid);

        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);
    }
}
