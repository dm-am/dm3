using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class CreateBlogValidatorShould
{
    private readonly CreateBlogValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateBlog
        {
            Title = "My Blog",
            Description = "Blog description",
            DraftVisibility = DraftVisibility.Public
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenDraftVisibilityIsNotInEnum()
    {
        var input = new CreateBlog
        {
            Title = "My Blog",
            DraftVisibility = (DraftVisibility)30000
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.DraftVisibility)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateBlog { Title = "" };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateBlog { Title = new string('a', BlogFieldLimits.BlogTitleMaxLength + 1) };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new CreateBlog
        {
            Title = new string('a', BlogFieldLimits.BlogTitleMaxLength),
            Description = "Description"
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
