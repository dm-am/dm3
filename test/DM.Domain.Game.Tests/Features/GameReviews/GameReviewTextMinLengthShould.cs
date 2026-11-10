using System;
using DM.Domain.Game.Features.GameReviews;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameReviews;

/// <remarks>
/// The text is stored trimmed, so the spaces around it do not count. An edit is
/// the other way a text creation refuses gets stored, so the case is put to both
/// validators.
/// </remarks>
public class GameReviewTextMinLengthShould
{
    private readonly CreateGameReviewValidator creation = new();
    private readonly UpdateGameReviewValidator edit = new();

    [Theory]
    [InlineData("Я")]
    [InlineData("   Я   ")]
    public void PassASingleCharacter(string text)
    {
        creation.TestValidate(new CreateGameReview { GameId = Guid.NewGuid(), Text = text })
            .ShouldNotHaveAnyValidationErrors();
        edit.TestValidate(new UpdateGameReview { ReviewId = Guid.NewGuid(), Text = text })
            .ShouldNotHaveAnyValidationErrors();
    }

    // Trimmed, a text of spaces is an empty review.
    [Fact]
    public void FailACreatedTextOfNothingButSpaces() =>
        creation.TestValidate(new CreateGameReview { GameId = Guid.NewGuid(), Text = "   " })
            .ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage("Введите текст рецензии");
}
