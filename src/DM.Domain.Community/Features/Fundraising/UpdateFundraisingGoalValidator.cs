using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Fundraising;

/// <inheritdoc />
internal class UpdateFundraisingGoalValidator : AbstractValidator<UpdateFundraisingGoal>
{
    /// <inheritdoc />
    public UpdateFundraisingGoalValidator()
    {
        RuleFor(g => g.GoalAmount)
            .GreaterThan(0).WithMessage(ValidationError.MustBePositive);

        RuleFor(g => g.CollectedAmount)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
