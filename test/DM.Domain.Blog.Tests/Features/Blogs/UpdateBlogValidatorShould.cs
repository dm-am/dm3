using System;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class UpdateBlogValidatorShould : UnitTestBase
{
    private readonly UpdateBlogValidator validator;

    public UpdateBlogValidatorShould()
    {
        validator = new UpdateBlogValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateBlog
        {
            BlogId = Guid.NewGuid(),
            Title = "Updated Blog",
            Description = "Updated description",
            DraftVisibility = DraftVisibility.Private,
            CommentsEnabled = false
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
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
            Title = new string('a', 201)
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
            Title = new string('a', 200)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
