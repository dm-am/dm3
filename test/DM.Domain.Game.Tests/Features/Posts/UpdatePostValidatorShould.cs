using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class UpdatePostValidatorShould : UnitTestBase
{
    private readonly UpdatePostValidator validator = new();

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
