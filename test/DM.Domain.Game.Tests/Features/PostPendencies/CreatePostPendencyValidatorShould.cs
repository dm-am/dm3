using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.PostPendencies;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

public class CreatePostPendencyValidatorShould : UnitTestBase
{
    private readonly CreatePostPendencyValidator validator;
    private readonly Mock<IUserLookupService> userLookupServiceMock;

    public CreatePostPendencyValidatorShould()
    {
        userLookupServiceMock = Mock<IUserLookupService>();
        validator = new CreatePostPendencyValidator(userLookupServiceMock.Object);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        userLookupServiceMock
            .Setup(s => s.UsernameExistsAsync("validuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            WaitingForUsername = "validuser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenRoomIdIsEmpty()
    {
        var input = new CreatePostPendency
        {
            RoomId = Guid.Empty,
            CharacterId = Guid.NewGuid(),
            WaitingForUsername = "validuser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.RoomId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenCharacterIdIsEmpty()
    {
        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.Empty,
            WaitingForUsername = "validuser"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.CharacterId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenWaitingForUsernameIsEmpty()
    {
        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            WaitingForUsername = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.WaitingForUsername)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenWaitingForUsernameIsNull()
    {
        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            WaitingForUsername = null!
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.WaitingForUsername);
    }

    [Fact]
    public async Task FailWhenUserDoesNotExist()
    {
        userLookupServiceMock
            .Setup(s => s.UsernameExistsAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            WaitingForUsername = "nonexistent"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.WaitingForUsername)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
