using DM.Domain.Community.Features.Fundraising;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Fundraising;

public class UpdateFundraisingGoalValidatorShould : UnitTestBase
{
    private readonly UpdateFundraisingGoalValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateFundraisingGoal
        {
            GoalAmount = 100m,
            CollectedAmount = 0m
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenGoalAmountIsZero()
    {
        var input = new UpdateFundraisingGoal
        {
            GoalAmount = 0m,
            CollectedAmount = 0m
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.GoalAmount)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void FailWhenGoalAmountIsNegative()
    {
        var input = new UpdateFundraisingGoal
        {
            GoalAmount = -1m,
            CollectedAmount = 0m
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.GoalAmount)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void FailWhenCollectedAmountIsNegative()
    {
        var input = new UpdateFundraisingGoal
        {
            GoalAmount = 100m,
            CollectedAmount = -1m
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.CollectedAmount)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassWhenCollectedAmountExceedsGoal()
    {
        var input = new UpdateFundraisingGoal
        {
            GoalAmount = 100m,
            CollectedAmount = 150m
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
