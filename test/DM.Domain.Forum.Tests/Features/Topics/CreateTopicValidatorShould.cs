using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using AwesomeAssertions;
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
        var input = new CreateTopic
        {
            Title = "Valid topic title",
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(t => t.Text);
    }
}
