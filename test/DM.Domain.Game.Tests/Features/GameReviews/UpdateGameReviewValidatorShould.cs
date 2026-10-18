using System;
using DM.Domain.Game.Features.GameReviews;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

public class UpdateGameReviewValidatorShould : UnitTestBase
{
    private readonly UpdateGameReviewValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateGameReview
        {
            ReviewId = Guid.NewGuid(),
            Text = "Updated review"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenTextIsOmitted()
    {
        var input = new UpdateGameReview
        {
            ReviewId = Guid.NewGuid(),
            Text = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Present and blank is not "keep what is stored": the service trims the text
    /// before storing it, so a text of spaces would publish an empty review.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("            ")]
    public void FailWhenTextIsBlank(string text)
    {
        var input = new UpdateGameReview
        {
            ReviewId = Guid.NewGuid(),
            Text = text
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Введите текст рецензии");
    }

    [Fact]
    public void FailWhenReviewIdIsEmpty()
    {
        var input = new UpdateGameReview
        {
            ReviewId = Guid.Empty
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ReviewId)
            .WithErrorMessage("Укажите рецензию");
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new UpdateGameReview
        {
            ReviewId = Guid.NewGuid(),
            Text = new string('a', 5001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Рецензия не длиннее 5000 символов");
    }
}
