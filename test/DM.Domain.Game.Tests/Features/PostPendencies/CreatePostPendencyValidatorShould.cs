using System;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.PostPendencies;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

/// <summary>
/// A pendency names a room and a character, and nothing else: the one who
/// waits and the one waited for are not the caller's to say.
/// </summary>
public class CreatePostPendencyValidatorShould : UnitTestBase
{
    private readonly CreatePostPendencyValidator validator = new();

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid()
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
            CharacterId = Guid.NewGuid()
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
            CharacterId = Guid.Empty
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.CharacterId)
            .WithErrorMessage(ValidationError.Empty);
    }
}
