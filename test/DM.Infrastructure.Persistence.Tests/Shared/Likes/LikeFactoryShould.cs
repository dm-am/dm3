using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Likes;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Likes;

public class LikeFactoryShould : UnitTestBase
{
    private readonly LikeFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;

    public LikeFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new LikeFactory(_guidFactory.Object);
    }

    [Fact]
    public void CreateLikeWithValidProperties()
    {
        var likeId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(likeId);

        var result = _factory.Create(entityId, LikeEntityType.Comment, userId);

        result.Should().NotBeNull();
        result.LikeId.Should().Be(likeId);
        result.UserId.Should().Be(userId);
        result.EntityId.Should().Be(entityId);
        result.EntityType.Should().Be(LikeEntityType.Comment);
    }

    [Fact]
    public void CreateCommentLike()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(entityId, LikeEntityType.Comment, userId);

        result.EntityType.Should().Be(LikeEntityType.Comment);
    }

    [Fact]
    public void CreatePostLike()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(entityId, LikeEntityType.Post, userId);

        result.EntityType.Should().Be(LikeEntityType.Post);
    }

    [Fact]
    public void CreateMessageLike()
    {
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(entityId, LikeEntityType.Message, userId);

        result.EntityType.Should().Be(LikeEntityType.Message);
    }

    [Fact]
    public void AssignUserIdCorrectly()
    {
        var userId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var result = _factory.Create(Guid.NewGuid(), LikeEntityType.Comment, userId);

        result.UserId.Should().Be(userId);
    }
}
