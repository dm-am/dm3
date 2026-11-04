using System;
using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Messaging.Features.Chats;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class CreateChatValidatorShould
{
    private readonly CreateChatValidator validator = new();

    private static CreateChat ValidChat(string title = "Valid chat title", Guid[]? participantIds = null) => new()
    {
        Title = title,
        ParticipantIds = participantIds ?? [Guid.NewGuid(), Guid.NewGuid()]
    };

    [Fact]
    public void PassForValidInput()
    {
        var input = ValidChat();

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = ValidChat(title: "");

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsNull()
    {
        var input = ValidChat(title: null!);

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = ValidChat(title: new string('x', ChatFieldLimits.TitleMaxLength + 1));

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenParticipantIdsAreEmpty()
    {
        var input = ValidChat(participantIds: Array.Empty<Guid>());

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ParticipantIds)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenParticipantIdsExceedLimit()
    {
        var input = ValidChat(participantIds: Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray());

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ParticipantIds)
            .WithErrorMessage(ValidationError.TooMany);
    }

    [Fact]
    public void PassWithMaximumParticipants()
    {
        var input = ValidChat(participantIds: Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray());

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
