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

public class CreateCommentValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit = Mock<IBbCodeNestingLimit>();
    private readonly CreateCommentValidator validator;

    public CreateCommentValidatorShould()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new CreateCommentValidator(nestingLimit);
    }

    // Text the renderer will refuse is refused here, or it is stored and shows as nothing to
    // everyone but its author.
    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var comment = new CreateComment { EntityId = Guid.NewGuid(), Text = "nested far too deep" };

        var result = validator.TestValidate(comment);

        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    // The Comment surface has no [private]; refused rather than erased, so the author rewrites the
    // draft.
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

    // A body past the length limit is refused, and refused by length rather than by some other rule
    // tripping over it.
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

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void FailWhenTextIsEmpty(string? text)
    {
        var comment = new CreateComment { EntityId = Guid.NewGuid(), Text = text! };

        var result = validator.TestValidate(comment);
        result.ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
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
}
