using System;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class UpdateRubricValidatorShould
{
    private readonly UpdateRubricValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = "Updated Rubric"
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenTitleIsNull()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = null
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenRubricIdIsEmpty()
    {
        var input = new UpdateRubric { RubricId = Guid.Empty };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.RubricId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.RubricTitleMaxLength + 1)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = new string('a', BlogFieldLimits.RubricTitleMaxLength)
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
