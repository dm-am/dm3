using System;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Messages;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class CreateMessageValidatorShould
{
    private readonly CreateMessageValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = "Valid message text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenChatIdIsEmpty()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.Empty,
            Text = "Valid text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ChatId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsNull()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = new string('x', BodyTextLimits.MaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = new string('x', BodyTextLimits.MaxLength)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // Refused rather than erased: a paragraph that vanished without a word is looked for, not
    // rewritten.
    [Fact]
    public void RefuseHidingMarkupTheSurfaceDoesNotDeclare()
    {
        var input = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после"
        };

        var result = validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(m => m.Text);
    }
}
