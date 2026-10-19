using System;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class UpdateBlogValidatorShould
{
    private readonly UpdateBlogValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            Title = "Updated Blog",
            Description = "Updated description",
            DraftVisibility = DraftVisibility.Private
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenDraftVisibilityIsNotInEnum()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            DraftVisibility = (DraftVisibility)30000
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.DraftVisibility)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassWhenDraftVisibilityIsOmitted()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            DraftVisibility = null
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.DraftVisibility);
    }

    [Fact]
    public void FailWhenBlogIdIsEmpty()
    {
        var input = new UpdateBlog { BlogId = Guid.Empty };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BlogId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmptyString()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            Title = ""
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.BlogTitleMaxLength + 1)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.BlogTitleMaxLength)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
