using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class CreateRoomAccessValidatorShould : UnitTestBase
{
    private readonly CreateRoomAccessValidator validator;
    private readonly Mock<IUserLookupService> userLookupServiceMock;

    public CreateRoomAccessValidatorShould()
    {
        userLookupServiceMock = Mock<IUserLookupService>();
        validator = new CreateRoomAccessValidator(userLookupServiceMock.Object);
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
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("reader", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

    [Fact]
    public async Task FailWhenReaderAccessPolicyIsNotReadOnly()
    {
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("reader", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            ReaderUsername = "reader",
            Policy = RoomAccessPolicy.Full
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.Policy)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
