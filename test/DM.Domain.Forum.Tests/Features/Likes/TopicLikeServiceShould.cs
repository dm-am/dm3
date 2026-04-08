using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Likes;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Likes;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Forum.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Likes;

public class TopicLikeServiceShould : UnitTestBase
{
    private readonly Mock<ITopicService> _topicService;
    private readonly Mock<ITopicCommentService> _commentService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ILikeOperations> _likeOperations;
    private readonly Mock<IUserBlacklistChecker> _blacklistChecker;
    private readonly TopicLikeService _service;

    public TopicLikeServiceShould()
    {
        _topicService = Mock<ITopicService>();
        _commentService = Mock<ITopicCommentService>();

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<TopicIntention>(), It.IsAny<Topic>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CommentIntention>(), It.IsAny<Comment>()));

        var userId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.Setup(o => o.LikeAsync(It.IsAny<Topic>(), It.IsAny<EventType>()))
            .ReturnsAsync(new GeneralUser());
        _likeOperations.Setup(o => o.LikeAsync(It.IsAny<Comment>(), It.IsAny<EventType>()))
            .ReturnsAsync(new GeneralUser());
        _likeOperations.Setup(o => o.UnlikeAsync(It.IsAny<Topic>()))
            .Returns(Task.CompletedTask);
        _likeOperations.Setup(o => o.UnlikeAsync(It.IsAny<Comment>()))
            .Returns(Task.CompletedTask);

        _blacklistChecker = Mock<IUserBlacklistChecker>();
        _blacklistChecker.Setup(c => c.IsBlockedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _service = new TopicLikeService(
            _topicService.Object,
            _commentService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _likeOperations.Object,
            _blacklistChecker.Object);
    }

    [Fact]
    public async Task AuthorizeLikeTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);

        await _service.LikeTopicAsync(topicId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(TopicIntention.Like, topic), Times.Once);
    }

    [Fact]
    public async Task LikeTopicAndPublishEvent()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        _likeOperations.Setup(o => o.LikeAsync(topic, EventType.LikedTopic))
            .ReturnsAsync(expectedUser);

        var result = await _service.LikeTopicAsync(topicId);

        result.Should().Be(expectedUser);
        _likeOperations.Verify(o => o.LikeAsync(topic, EventType.LikedTopic), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenLikingTopicByBlockedUser()
    {
        var topicId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Object.Current.User.UserId;
        var topic = new Topic
        {
            Id = topicId,
            Author = new GeneralUser { UserId = authorId }
        };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        _blacklistChecker.Setup(c => c.IsBlockedAsync(authorId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _service.LikeTopicAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("You cannot like this topic");
    }

    [Fact]
    public async Task AuthorizeLikeCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);

        await _service.LikeCommentAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Like, comment), Times.Once);
    }

    [Fact]
    public async Task LikeCommentAndPublishEvent()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _likeOperations.Setup(o => o.LikeAsync(comment, EventType.LikedTopicComment))
            .ReturnsAsync(expectedUser);

        var result = await _service.LikeCommentAsync(commentId);

        result.Should().Be(expectedUser);
        _likeOperations.Verify(o => o.LikeAsync(comment, EventType.LikedTopicComment), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenLikingCommentByBlockedUser()
    {
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Object.Current.User.UserId;
        var comment = new Comment
        {
            Id = commentId,
            Author = new GeneralUser { UserId = authorId }
        };
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _blacklistChecker.Setup(c => c.IsBlockedAsync(authorId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _service.LikeCommentAsync(commentId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("You cannot like this comment");
    }

    [Fact]
    public async Task UnlikeTopic()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);

        await _service.UnlikeTopicAsync(topicId);

        _likeOperations.Verify(o => o.UnlikeAsync(topic), Times.Once);
    }

    [Fact]
    public async Task UnlikeComment()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);

        await _service.UnlikeCommentAsync(commentId);

        _likeOperations.Verify(o => o.UnlikeAsync(comment), Times.Once);
    }
}
