using System;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Comments;

public class UpdateCommentValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit = Mock<IBbCodeNestingLimit>();
    private readonly UpdateCommentValidator validator;

    public UpdateCommentValidatorShould()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new UpdateCommentValidator(nestingLimit);
    }

    // An edit is how most unrenderable text would arrive, so the edit path refuses it too.
    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var comment = new UpdateComment { CommentId = Guid.NewGuid(), Text = "nested far too deep" };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    // The edit path enforces the same length limit as creation, or the limit is one save away from
    // being void.
    [Fact]
    public void RefuseTextLongerThanTheLimit()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = new string('x', BodyTextLimits.MaxLength + 1)
        };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassForValidComment()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = "This is an updated comment text"
        };

        var result = validator.TestValidate(comment);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenCommentIdIsEmpty()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.Empty,
            Text = "Valid text"
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.CommentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void FailWhenTextIsEmpty(string? text)
    {
        var comment = new UpdateComment { CommentId = Guid.NewGuid(), Text = text };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    // The tag is not markup here; refused rather than erased, so the author rewrites the draft.
    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text);
    }
}
