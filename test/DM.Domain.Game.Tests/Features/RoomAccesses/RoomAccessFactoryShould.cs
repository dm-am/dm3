using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class RoomAccessFactoryShould : UnitTestBase
{
    private readonly RoomAccessFactory factory;
    private readonly IGuidFactory guidFactory;

    public RoomAccessFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();

        factory = new RoomAccessFactory(guidFactory);
    }

    [Fact]
    public void CreateRoomAccessForCharacter()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();

        var createRoomAccess = new CreateRoomAccess
        {
            RoomId = roomId,
            Policy = RoomAccessPolicy.Full
        };

        guidFactory.Create().Returns(accessId);

        var result = factory.CreateForCharacter(createRoomAccess, characterId);

        result.AccessId.Should().Be(accessId);
        result.RoomId.Should().Be(roomId);
        result.Policy.Should().Be(RoomAccessPolicy.Full);
        result.CharacterId.Should().Be(characterId);
        result.ReaderUserId.Should().BeNull();
    }

    [Fact]
    public void CreateRoomAccessForReader()
    {
        var accessId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var readerUserId = Guid.NewGuid();

        var createRoomAccess = new CreateRoomAccess
        {
            RoomId = roomId,
            Policy = RoomAccessPolicy.ReadOnly
        };

        guidFactory.Create().Returns(accessId);

        var result = factory.CreateForReader(createRoomAccess, readerUserId);

        result.AccessId.Should().Be(accessId);
        result.RoomId.Should().Be(roomId);
        result.Policy.Should().Be(RoomAccessPolicy.ReadOnly);
        result.ReaderUserId.Should().Be(readerUserId);
        result.CharacterId.Should().BeNull();
    }
}
