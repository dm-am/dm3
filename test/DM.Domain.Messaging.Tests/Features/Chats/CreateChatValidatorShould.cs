using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class CreateChatValidatorShould : UnitTestBase
{
    private readonly CreateChatValidator validator;

    public CreateChatValidatorShould()
    {
        validator = new CreateChatValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateChat
        {
            Title = "Valid chat title",
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateChat
        {
            Title = "",
            ParticipantIds = new[] { Guid.NewGuid() }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsNull()
    {
        var input = new CreateChat
        {
            Title = null!,
            ParticipantIds = new[] { Guid.NewGuid() }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateChat
        {
            Title = new string('x', 201),
            ParticipantIds = new[] { Guid.NewGuid() }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenParticipantIdsAreEmpty()
    {
        var input = new CreateChat
        {
            Title = "Valid title",
            ParticipantIds = Array.Empty<Guid>()
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ParticipantIds)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenParticipantIdsExceedLimit()
    {
        var input = new CreateChat
        {
            Title = "Valid title",
            ParticipantIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray()
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ParticipantIds)
            .WithErrorMessage(ValidationError.TooMany);
    }

    [Fact]
    public void PassWithMaximumParticipants()
    {
        var input = new CreateChat
        {
            Title = "Valid title",
            ParticipantIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray()
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
