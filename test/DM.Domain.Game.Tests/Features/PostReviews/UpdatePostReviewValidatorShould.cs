using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostReviews;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostReviews;

public class UpdatePostReviewValidatorShould : UnitTestBase
{
    private readonly UpdatePostReviewValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdatePostReview
        {
            ReviewId = Guid.NewGuid(),
            Sign = ReviewSign.Positive
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenSignIsOmitted()
    {
        var input = new UpdatePostReview
        {
            ReviewId = Guid.NewGuid(),
            Sign = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenSignIsNotInEnum()
    {
        var input = new UpdatePostReview
        {
            ReviewId = Guid.NewGuid(),
            Sign = (ReviewSign)30000
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Sign)
            .WithErrorMessage("Оценка указана неверно");
    }

    [Fact]
    public void FailWhenReviewIdIsEmpty()
    {
        var input = new UpdatePostReview
        {
            ReviewId = Guid.Empty
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.ReviewId)
            .WithErrorMessage("Укажите рецензию");
    }
}
