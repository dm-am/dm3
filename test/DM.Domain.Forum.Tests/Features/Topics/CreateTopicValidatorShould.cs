using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class CreateTopicValidatorShould : UnitTestBase
{
    private readonly CreateTopicValidator validator;

    public CreateTopicValidatorShould()
    {
        validator = new CreateTopicValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateTopic
        {
            Title = "Valid topic title"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateTopic
        {
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsNull()
    {
        var input = new CreateTopic
        {
            Title = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void FailWhenTitleIsWhitespace()
    {
        var input = new CreateTopic
        {
            Title = "   "
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateTopic
        {
            Title = new string('x', 131)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new CreateTopic
        {
            Title = new string('x', 130)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
