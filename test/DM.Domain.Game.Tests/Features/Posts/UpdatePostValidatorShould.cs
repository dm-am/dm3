using System;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class UpdatePostValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit = Mock<IBbCodeNestingLimit>();
    private readonly UpdatePostValidator validator;

    public UpdatePostValidatorShould()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new UpdatePostValidator(nestingLimit);
    }

    /// <summary>
    /// An edit is how most unrenderable text would arrive, so the edit path
    /// refuses it too.
    /// </summary>
    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var input = new UpdatePost
        {
            PostId = Guid.NewGuid(),
            GameText = "nested far too deep"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(p => p.GameText)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// The edit path enforces the same length limit as creation, or the limit
    /// is one save away from being void.
    /// </summary>
    [Fact]
    public void RefuseTextLongerThanTheLimit()
    {
        var input = new UpdatePost
        {
            PostId = Guid.NewGuid(),
            GameText = new string('x', BodyTextLimits.MaxLength + 1)
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(p => p.GameText)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdatePost
        {
            PostId = Guid.NewGuid(),
            GameText = "Updated game text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenGameTextIsOmitted()
    {
        var input = new UpdatePost
        {
            PostId = Guid.NewGuid(),
            GameText = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenPostIdIsEmpty()
    {
        var input = new UpdatePost
        {
            PostId = Guid.Empty,
            GameText = "Updated game text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(p => p.PostId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenGameTextIsEmpty()
    {
        var input = new UpdatePost
        {
            PostId = Guid.NewGuid(),
            GameText = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(p => p.GameText)
            .WithErrorMessage(ValidationError.Empty);
    }
}
