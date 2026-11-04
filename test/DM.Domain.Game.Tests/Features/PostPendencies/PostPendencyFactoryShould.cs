using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.PostPendencies;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostPendencies;

public class PostPendencyFactoryShould : UnitTestBase
{
    private readonly PostPendencyFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;

    public PostPendencyFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        factory = new PostPendencyFactory(
            guidFactory,
            dateTimeProvider);
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

        guidFactory.Create().Returns(pendencyId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.Create(createPostPendency, createdById, waitingForUserId);

        result.Should().NotBeNull();
        result.PendencyId.Should().Be(pendencyId);
        result.RoomId.Should().Be(roomId);
        result.CharacterId.Should().Be(characterId);
        result.CreatedById.Should().Be(createdById);
        result.WaitingForUserId.Should().Be(waitingForUserId);
        result.CreatedUtc.Should().Be(now);
    }
}
