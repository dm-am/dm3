using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostPendencies;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

public class PostPendencyFactoryShould : UnitTestBase
{
    private readonly PostPendencyFactory _factory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PostPendencyFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new PostPendencyFactory(
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public void CreatePostPendencyWithValidProperties()
    {
        var pendencyId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createdById = Guid.NewGuid();
        var waitingForUserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var createPostPendency = new CreatePostPendency
        {
            RoomId = roomId,
            CharacterId = characterId
        };

        _guidFactory.Create().Returns(pendencyId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.Create(createPostPendency, createdById, waitingForUserId);

        result.Should().NotBeNull();
        result.PendencyId.Should().Be(pendencyId);
        result.RoomId.Should().Be(roomId);
        result.CharacterId.Should().Be(characterId);
        result.CreatedById.Should().Be(createdById);
        result.WaitingForUserId.Should().Be(waitingForUserId);
        result.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public void AssignCreatedByIdCorrectly()
    {
        var createdById = Guid.NewGuid();
        var waitingForUserId = Guid.NewGuid();
        var createPostPendency = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid()
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createPostPendency, createdById, waitingForUserId);

        result.CreatedById.Should().Be(createdById);
    }

    [Fact]
    public void AssignWaitingForUserIdCorrectly()
    {
        var createdById = Guid.NewGuid();
        var waitingForUserId = Guid.NewGuid();
        var createPostPendency = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid()
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createPostPendency, createdById, waitingForUserId);

        result.WaitingForUserId.Should().Be(waitingForUserId);
    }

    [Fact]
    public void GenerateUniquePendencyId()
    {
        var createPostPendency = new CreatePostPendency
        {
            RoomId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid()
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createPostPendency, Guid.NewGuid(), Guid.NewGuid());

        result.PendencyId.Should().NotBeEmpty();
    }
}
