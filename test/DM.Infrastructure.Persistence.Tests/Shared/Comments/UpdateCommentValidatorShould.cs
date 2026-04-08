using System;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Comments;

public class UpdateCommentValidatorShould : UnitTestBase
{
    private readonly UpdateCommentValidator validator;

    public UpdateCommentValidatorShould()
    {
        validator = new UpdateCommentValidator();
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
}
