using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class CreateMessageValidatorShould : UnitTestBase
{
    private readonly CreateMessageValidator validator;

    public CreateMessageValidatorShould()
    {
        validator = new CreateMessageValidator();
    }

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
            Text = new string('x', 50001)
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
            Text = new string('x', 50000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
