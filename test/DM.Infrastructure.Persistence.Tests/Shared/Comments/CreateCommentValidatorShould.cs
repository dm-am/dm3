using System;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Comments;

public class CreateCommentValidatorShould : UnitTestBase
{
    private readonly CreateCommentValidator validator;

    public CreateCommentValidatorShould()
    {
        validator = new CreateCommentValidator();
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
