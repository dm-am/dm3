using DM.Domain.Community.Features.Fundraising;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Fundraising;

public class UpdateFundraisingGoalValidatorShould : UnitTestBase
{
    private readonly UpdateFundraisingGoalValidator validator = new();

    /// <summary>
    /// A goal that passes every rule, so each test below states the one field
    /// it is about instead of restating the other two.
    /// </summary>
    private static UpdateFundraisingGoal Goal(
        string title = "Хостинг и домен на год",
        decimal goalAmount = 100m,
        decimal collectedAmount = 0m) => new()
        {
            Title = title,
            GoalAmount = goalAmount,
            CollectedAmount = collectedAmount
        };

    [Fact]
    public void PassForValidInput()
    {
        var result = validator.TestValidate(Goal());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var result = validator.TestValidate(Goal(title: string.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsWhitespace()
    {
        var result = validator.TestValidate(Goal(title: "   "));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsLongerThanTheColumn()
    {
        // 201 characters: one past the column the title is stored in, so the
        // rule refuses what the database would have truncated or thrown on.
        var result = validator.TestValidate(Goal(title: new string('a', 201)));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleFillsTheColumnExactly()
    {
        var result = validator.TestValidate(Goal(title: new string('a', 200)));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenGoalAmountIsZero()
    {
        var result = validator.TestValidate(Goal(goalAmount: 0m));
        result.ShouldHaveValidationErrorFor(x => x.GoalAmount)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void FailWhenGoalAmountIsNegative()
    {
        var result = validator.TestValidate(Goal(goalAmount: -1m));
        result.ShouldHaveValidationErrorFor(x => x.GoalAmount)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void FailWhenCollectedAmountIsNegative()
    {
        var result = validator.TestValidate(Goal(collectedAmount: -1m));
        result.ShouldHaveValidationErrorFor(x => x.CollectedAmount)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassWhenCollectedAmountExceedsGoal()
    {
        var result = validator.TestValidate(Goal(goalAmount: 100m, collectedAmount: 150m));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
