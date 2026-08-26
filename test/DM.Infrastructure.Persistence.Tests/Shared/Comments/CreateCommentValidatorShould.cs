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

public class CreateCommentValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit = Mock<IBbCodeNestingLimit>();
    private readonly CreateCommentValidator validator;

    public CreateCommentValidatorShould()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new CreateCommentValidator(nestingLimit);
    }

    /// <summary>
    /// Text the renderer will refuse is refused here, or it is stored and shows
    /// as nothing to everyone but its author.
    /// </summary>
    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var comment = new CreateComment { EntityId = Guid.NewGuid(), Text = "nested far too deep" };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// Hiding markup on a surface that does not declare it.
    /// </summary>
    /// <remarks>
    /// A comment renders on the Comment surface, whose tag set has no [private]
    /// at all, so the tag is not markup here and the line the author meant to
    /// hide is published with the tag still around it. Refused at the save path
    /// rather than erased: the author is writing a draft, not attacking a
    /// reader, and a paragraph that vanished without a word is looked for
    /// instead of rewritten.
    /// </remarks>
    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text);
    }

    /// <summary>
    /// A body past the length limit is refused, and refused by length rather
    /// than by some other rule tripping over it.
    /// </summary>
    /// <remarks>
    /// Comments had no limit at all, so the parse and the render of every open
    /// of the page were bounded by the request size and nothing else.
    /// </remarks>
    [Fact]
    public void RefuseTextLongerThanTheLimit()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = new string('x', BodyTextLimits.MaxLength + 1)
        };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassForTextExactlyAtTheLimit()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = new string('x', BodyTextLimits.MaxLength)
        };

        var result = validator.TestValidate(comment);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassForValidComment()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = "This is a valid comment text"
        };

        var result = validator.TestValidate(comment);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsNull()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = null!
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text);
    }

    [Fact]
    public void FailWhenTextIsWhitespace()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = "   "
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text);
    }

    [Fact]
    public void FailWhenEntityIdIsEmpty()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.Empty,
            Text = "Valid text"
        };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.EntityId);
    }

    [Fact]
    public void PassWithMinimalValidInput()
    {
        var comment = new CreateComment
        {
            EntityId = Guid.NewGuid(),
            Text = "A"
        };

        var result = validator.TestValidate(comment);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
