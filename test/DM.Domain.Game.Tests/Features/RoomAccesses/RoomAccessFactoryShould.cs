using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.RoomAccesses;

public class RoomAccessFactoryShould : UnitTestBase
{
    private readonly RoomAccessFactory _factory;
    private readonly IGuidFactory _guidFactory;

    public RoomAccessFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new RoomAccessFactory(_guidFactory);
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

        _guidFactory.Create().Returns(accessId);

        var result = _factory.CreateForCharacter(createRoomAccess, characterId);

        result.Should().NotBeNull();
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

        _guidFactory.Create().Returns(accessId);

        var result = _factory.CreateForReader(createRoomAccess, readerUserId);

        result.Should().NotBeNull();
        result.AccessId.Should().Be(accessId);
        result.RoomId.Should().Be(roomId);
        result.Policy.Should().Be(RoomAccessPolicy.ReadOnly);
        result.ReaderUserId.Should().Be(readerUserId);
        result.CharacterId.Should().BeNull();
    }

    [Fact]
    public void PreserveRoomAccessPolicy()
    {
        var createRoomAccess = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            Policy = RoomAccessPolicy.Full
        };

        _guidFactory.Create().Returns(Guid.NewGuid());

        var result = _factory.CreateForCharacter(createRoomAccess, Guid.NewGuid());

        result.Policy.Should().Be(RoomAccessPolicy.Full);
    }

    [Fact]
    public void GenerateUniqueAccessIds()
    {
        var accessId1 = Guid.NewGuid();
        var accessId2 = Guid.NewGuid();
        var currentIndex = 0;
        var accessIds = new[] { accessId1, accessId2 };

        _guidFactory.Create().Returns(_ => accessIds[currentIndex++]);

        var createRoomAccess = new CreateRoomAccess
        {
            RoomId = Guid.NewGuid(),
            Policy = RoomAccessPolicy.Full
        };

        var result1 = _factory.CreateForCharacter(createRoomAccess, Guid.NewGuid());
        var result2 = _factory.CreateForReader(createRoomAccess, Guid.NewGuid());

        result1.AccessId.Should().NotBe(result2.AccessId);
    }
}
