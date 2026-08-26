using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class UpdateChatValidatorShould : UnitTestBase
{
    private readonly UpdateChatValidator validator;

    public UpdateChatValidatorShould()
    {
        validator = new UpdateChatValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateChat
        {
            ChatId = Guid.NewGuid(),
            Title = "Updated chat title"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenChatIdIsEmpty()
    {
        var input = new UpdateChat
        {
            ChatId = Guid.Empty,
            Title = "Valid title"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ChatId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWhenTitleIsNull()
    {
        var input = new UpdateChat
        {
            ChatId = Guid.NewGuid(),
            Title = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateChat
        {
            ChatId = Guid.NewGuid(),
            Title = new string('x', 201)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new UpdateChat
        {
            ChatId = Guid.NewGuid(),
            Title = new string('x', 200)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
