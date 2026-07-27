using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostReviews;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostReviews;

public class CreatePostReviewValidatorShould : UnitTestBase
{
    private readonly CreatePostReviewValidator validator = new();

    [Theory]
    [InlineData(ReviewSign.Negative)]
    [InlineData(ReviewSign.Neutral)]
    [InlineData(ReviewSign.Positive)]
    public void PassForValidInput(ReviewSign sign)
    {
        var input = new CreatePostReview
        {
            PostId = Guid.NewGuid(),
            Sign = sign,
            Text = "Great post"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenPostIdIsEmpty()
    {
        var input = new CreatePostReview
        {
            PostId = Guid.Empty,
            Sign = ReviewSign.Positive,
            Text = "Great post"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.PostId)
            .WithErrorMessage("Post ID is required");
    }

    [Fact]
    public void FailWhenSignIsNotInEnum()
    {
        var input = new CreatePostReview
        {
            PostId = Guid.NewGuid(),
            Sign = (ReviewSign)99,
            Text = "Great post"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Sign)
            .WithErrorMessage("Invalid review sign");
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreatePostReview
        {
            PostId = Guid.NewGuid(),
            Sign = ReviewSign.Positive,
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(r => r.Text)
            .WithErrorMessage("Review text is required");
    }
}
