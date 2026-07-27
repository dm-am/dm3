using System;
using DM.Domain.Game.Features.GameReviews;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class CreateGameReviewValidatorShould : UnitTestBase
{
    private readonly CreateGameReviewValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateGameReview
        {
            GameId = Guid.NewGuid(),
            Text = "Wonderful game"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenGameIdIsEmpty()
    {
        var input = new CreateGameReview
        {
            GameId = Guid.Empty,
            Text = "Wonderful game"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("Game ID is required");
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateGameReview
        {
            GameId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Review text is required");
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new CreateGameReview
        {
            GameId = Guid.NewGuid(),
            Text = new string('a', 5001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Review text must not exceed 5000 characters");
    }
}
