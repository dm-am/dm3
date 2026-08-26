using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;
using AwesomeAssertions;
using Xunit;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;

namespace DM.Web.API.Tests.Features.Game;

public class RoomMapperShould : UnitTestBase
{
    private RoomMapper CreateMapper()
    {
        var userMapper = new UserMapper(Mock<IImgproxyUrlBuilder>());
        return new RoomMapper(userMapper, new CharacterMapper(userMapper));
    }

    /// <summary>
    /// The policy is what decides who may write in the room. It was mapped with
    /// Ignore(), so every access of every response answered null: the screen that
    /// grants access could not show which grant it had just made, and no client
    /// could tell a spectator from a participant.
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
    /// PATCH carries "not sent" as null and the write model must keep saying
    /// it: the repository applies each setting only when its nullable is set,
    /// so a mapper that defaults an omitted field re-introduces the bug where
    /// renaming a room silently rewrote its access and dice settings.
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
    /// PreviousRoomId is three-valued: absent (covered above), explicit null
    /// (move to the head of the chain) and a value. The wrapped null must
    /// survive the map - collapsing it to a bare null turns "move to head"
    /// into "do not reorder".
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
