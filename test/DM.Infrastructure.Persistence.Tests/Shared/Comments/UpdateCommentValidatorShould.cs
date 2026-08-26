using System;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Testing;
using AwesomeAssertions;
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

    /// <summary>
    /// An edit is how most unrenderable text would arrive, so the edit path
    /// refuses it too.
    /// </summary>
    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var comment = new UpdateComment { CommentId = Guid.NewGuid(), Text = "nested far too deep" };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// The edit path enforces the same length limit as creation, or the limit
    /// is one save away from being void.
    /// </summary>
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

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsNull()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = null
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text);
    }

    [Fact]
    public void FailWhenTextIsWhitespace()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = "   "
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text);
    }

    [Fact]
    public void PassWithMinimalValidInput()
    {
        var comment = new UpdateComment
        {
            CommentId = Guid.NewGuid(),
            Text = "A"
        };

        var result = validator.TestValidate(comment);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Hiding markup on a surface that does not declare it.
    /// </summary>
    /// <remarks>
    /// The tag is not markup here, so it hides nothing: the line the author
    /// wrote for one reader is published with the tag still around it. Refused
    /// at the save path rather than erased, because a draft is not an attack
    /// and a paragraph that vanished without a word is looked for instead of
    /// rewritten.
    /// </remarks>
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
