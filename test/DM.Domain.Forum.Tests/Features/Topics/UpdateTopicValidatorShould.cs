using System;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Features.Topics;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class UpdateTopicValidatorShould
{
    private readonly UpdateTopicValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Title = "Updated topic title"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTopicIdIsEmpty()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.Empty,
            Title = "Valid title"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.TopicId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWhenTitleIsNull()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Title = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Title = new string('x', TopicPolicy.TitleMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Title = new string('x', TopicPolicy.TitleMaxLength)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var input = new UpdateTopic
        {
            TopicId = Guid.NewGuid(),
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(t => t.Text);
    }
}
