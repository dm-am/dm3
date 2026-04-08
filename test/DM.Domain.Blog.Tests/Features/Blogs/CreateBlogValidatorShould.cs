using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class CreateBlogValidatorShould : UnitTestBase
{
    private readonly CreateBlogValidator validator;

    public CreateBlogValidatorShould()
    {
        validator = new CreateBlogValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateBlog
        {
            Title = "My Blog",
            Description = "Blog description",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
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
        var input = new CreateBlog { Title = new string('a', 201) };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new CreateBlog
        {
            Title = new string('a', 200),
            Description = "Description"
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
