using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;
using AwesomeAssertions;
using Xunit;
using DomainRoomAccess = DM.Domain.Game.Features.RoomAccesses.RoomAccess;

namespace DM.Web.API.Tests.Features.Game;

public class RoomMapperShould : UnitTestBase
{
    private static RoomMapper CreateMapper()
    {
        var userMapper = new UserMapper();
        return new RoomMapper(userMapper, new CharacterMapper(userMapper));
    }

    /// <summary>
    /// The policy decides who may write in the room, and every response carries it.
    /// </summary>
    [Fact]
    public void CarryTheAccessPolicy()
    {
        var access = CreateMapper().ToRoomAccess(new DomainRoomAccess
        {
            Id = Guid.NewGuid(),
            TargetType = RoomAccessTargetType.Reader,
            Policy = RoomAccessPolicy.ReadOnly
        });

        access.Policy.Should().Be(RoomAccessPolicy.ReadOnly);
    }

    /// <summary>
    /// PATCH carries "not sent" as null and the write model keeps saying it: each setting applies only when set.
    /// </summary>
    [Fact]
    public void KeepAnUnsentUpdateFieldNull()
    {
        var update = CreateMapper().ToUpdateRoom(new UpdateRoomRequest
        {
            Title = "Только имя"
        });

        update.Title.Should().Be("Только имя");
        update.Type.Should().BeNull();
        update.AccessType.Should().BeNull();
        update.IsArchived.Should().BeNull();
        update.PreviousRoomId.Should().BeNull("absent means \"do not reorder\"");
        update.ViewPrivateText.Should().BeNull();
        update.ViewDiceResults.Should().BeNull();
        update.DiceEnabled.Should().BeNull();
        update.HiddenWithoutAccess.Should().BeNull();
    }

    [Fact]
    public void FlattenASentSettingsBlock()
    {
        var update = CreateMapper().ToUpdateRoom(new UpdateRoomRequest
        {
            Access = RoomAccessType.Private,
            Settings = new RoomSettings
            {
                ViewPrivateText = true,
                ViewDiceResults = false,
                DiceEnabled = true,
                HiddenWithoutAccess = true
            }
        });

        update.AccessType.Should().Be(RoomAccessType.Private);
        update.ViewPrivateText.Should().BeTrue();
        update.ViewDiceResults.Should().BeFalse();
        update.DiceEnabled.Should().BeTrue();
        update.HiddenWithoutAccess.Should().BeTrue();
    }

    /// <summary>
    /// PreviousRoomId is three-valued: absent, explicit null (move to the head) and a value.
    /// </summary>
    [Fact]
    public void CarryAnExplicitNullPreviousRoomThrough()
    {
        var update = CreateMapper().ToUpdateRoom(new UpdateRoomRequest
        {
            PreviousRoomId = Optional<Guid>.WithValue(null)
        });

        update.PreviousRoomId.Should().NotBeNull();
        update.PreviousRoomId!.Value.Should().BeNull();
    }
}
