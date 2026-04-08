using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class UpdateMessageValidatorShould : UnitTestBase
{
    private readonly UpdateMessageValidator validator;

    public UpdateMessageValidatorShould()
    {
        validator = new UpdateMessageValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.NewGuid(),
            Text = "Updated message text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenMessageIdIsEmpty()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.Empty,
            Text = "Valid text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.MessageId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWhenTextIsNull()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.NewGuid(),
            Text = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.NewGuid(),
            Text = new string('x', 50001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new UpdateMessage
        {
            MessageId = Guid.NewGuid(),
            Text = new string('x', 50000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
