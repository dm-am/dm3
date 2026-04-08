using System;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class UpdateRubricValidatorShould : UnitTestBase
{
    private readonly UpdateRubricValidator _validator;

    public UpdateRubricValidatorShould()
    {
        _validator = new UpdateRubricValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = "Updated Rubric"
        };
        var result = _validator.TestValidate(input);
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
        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenRubricIdIsEmpty()
    {
        var input = new UpdateRubric { RubricId = Guid.Empty };
        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.RubricId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = new string('a', 101)
        };
        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTitleIsAtMaxLength()
    {
        var input = new UpdateRubric
        {
            RubricId = Guid.NewGuid(),
            Title = new string('a', 100)
        };
        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
