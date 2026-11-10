using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Features.Topics;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class CreateTopicValidatorShould
{
    private readonly CreateTopicValidator validator = new();

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
            Title = new string('x', TopicPolicy.TitleMaxLength + 1)
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
            Title = new string('x', TopicPolicy.TitleMaxLength)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var input = new CreateTopic
        {
            Title = "Valid topic title",
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(t => t.Text);
    }
}
