using System;
using DM.Domain.Game.Features.GameReviews;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class CreateGameReviewValidatorShould
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
            .WithErrorMessage("Укажите игру");
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
            .WithErrorMessage("Введите текст рецензии");
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new CreateGameReview
        {
            GameId = Guid.NewGuid(),
            Text = new string('a', GameReviewFieldLimits.TextMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Рецензия не длиннее 5000 символов");
    }
}
