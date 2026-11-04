using System;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class CreateRubricValidatorShould
{
    private readonly CreateRubricValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateRubric
        {
            BlogId = Guid.NewGuid(),
            Title = "My Rubric",
            SortOrder = 1
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenBlogIdIsEmpty()
    {
        var input = new CreateRubric { BlogId = Guid.Empty };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BlogId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateRubric
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
        var input = new CreateRubric
        {
            BlogId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.RubricTitleMaxLength + 1)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new CreateRubric
        {
            BlogId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.RubricTitleMaxLength)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
