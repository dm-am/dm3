using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Validator for a change of warning points
/// </summary>
/// <remarks>
/// The explanation is required: everybody who reads the log sees it, and without it
/// the original verdict stands beside points that contradict it.
/// </remarks>
internal class ChangeWarningPointsValidator : AbstractValidator<ChangeWarningPoints>
{
    public ChangeWarningPointsValidator()
    {
        RuleFor(c => c.Points)
            .NotNull().WithMessage(ValidationError.Empty)
            .InclusiveBetween(0, WarningPolicy.MaxPointsPerWarning).WithMessage(ValidationError.Invalid);

        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(WarningPolicy.ReasonMaxLength).WithMessage(ValidationError.Long);
    }
}
