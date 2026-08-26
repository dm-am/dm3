using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class UpdateTopicValidatorShould : UnitTestBase
{
    private readonly UpdateTopicValidator validator;

    public UpdateTopicValidatorShould()
    {
        validator = new UpdateTopicValidator();
    }

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
            Title = new string('x', 131)
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
            Title = new string('x', 130)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Hiding markup on a surface that does not declare it.
    /// </summary>
    /// <remarks>
    /// The tag is not markup here, so it hides nothing: the line the author
    /// wrote for one reader is published with the tag still around it. Refused
    /// at the save path rather than erased, because a draft is not an attack
    /// and a paragraph that vanished without a word is looked for instead of
    /// rewritten.
    /// </remarks>
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
