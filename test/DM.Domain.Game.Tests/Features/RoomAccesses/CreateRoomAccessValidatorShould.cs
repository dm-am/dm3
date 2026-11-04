using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class CreateRoomAccessValidatorShould : UnitTestBase
{
    private readonly CreateRoomAccessValidator validator;
    private readonly IUserLookupService userLookupService;

    public CreateRoomAccessValidatorShould()
    {
        userLookupService = Mock<IUserLookupService>();
        validator = new CreateRoomAccessValidator(userLookupService);
    }

    [Fact]
    public async Task PassForValidCharacterAccess()
    {
        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Policy = RoomAccessPolicy.Full
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task PassForValidReaderAccess()
    {
        userLookupService
            .UsernameExistsAsync("reader", Arg.Any<CancellationToken>()).Returns(true);

        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            ReaderUsername = "reader",
            Policy = RoomAccessPolicy.ReadOnly
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenPolicyIsNotInEnum()
    {
        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Policy = (RoomAccessPolicy)30000
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.Policy)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenPolicyIsNoAccess()
    {
        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Policy = RoomAccessPolicy.NoAccess
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.Policy)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenCharacterIdIsEmptyGuid()
    {
        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.Empty,
            Policy = RoomAccessPolicy.Full
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("CharacterId.Value")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenReaderUsernameDoesNotExist()
    {
        userLookupService
            .UsernameExistsAsync("nonexistent", Arg.Any<CancellationToken>()).Returns(false);

        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            ReaderUsername = "nonexistent",
            Policy = RoomAccessPolicy.ReadOnly
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.ReaderUsername)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// A reader row takes both policies: the policy decides whether the reader speaks in the chat.
    /// </summary>
    [Fact]
    public async Task PassForAReaderGrantedWriting()
    {
        userLookupService
            .UsernameExistsAsync("reader", Arg.Any<CancellationToken>()).Returns(true);

        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            ReaderUsername = "reader",
            Policy = RoomAccessPolicy.Full
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
