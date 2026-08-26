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
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Likes;

public class TopicLikeServiceShould : UnitTestBase
{
    private readonly ITopicService _topicService;
    private readonly ITopicCommentService _commentService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly TopicLikeService _service;

    public TopicLikeServiceShould()
    {
        _topicService = Mock<ITopicService>();
        _commentService = Mock<ITopicCommentService>();

        _intentionManager = Mock<IIntentionManager>();

        var userId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.LikeAsync(Arg.Any<Topic>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        _likeOperations.LikeAsync(Arg.Any<Comment>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        _likeOperations.UnlikeAsync(Arg.Any<Topic>()).Returns(Task.CompletedTask);
        _likeOperations.UnlikeAsync(Arg.Any<Comment>()).Returns(Task.CompletedTask);

        _blacklistChecker = Mock<IUserBlacklistChecker>();
        _blacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        _service = new TopicLikeService(
            _topicService,
            _commentService,
            _intentionManager,
            _identityProvider,
            _likeOperations,
            _blacklistChecker);
    }

    [Fact]
    public async Task AuthorizeLikeTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        await _service.LikeTopicAsync(topicId);

        _intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Like, topic);
    }

    [Fact]
    public async Task LikeTopicAndPublishEvent()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        _likeOperations.LikeAsync(topic, EventType.LikedTopic).Returns(expectedUser);

        var result = await _service.LikeTopicAsync(topicId);

        result.Should().Be(expectedUser);
        await _likeOperations.Received(1).LikeAsync(topic, EventType.LikedTopic);
    }

    [Fact]
    public async Task ThrowWhenLikingTopicByBlockedUser()
    {
        var topicId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Current.User.UserId;
        var topic = new Topic
        {
            Id = topicId,
            Author = new GeneralUser { UserId = authorId }
        };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        _blacklistChecker.IsBlockedAsync(authorId, currentUserId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.LikeTopicAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("Вы не можете поставить лайк этому топику");
    }

    [Fact]
    public async Task AuthorizeLikeCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        _commentService.GetAsync(commentId).Returns(comment);

        await _service.LikeCommentAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task LikeCommentAndPublishEvent()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        _commentService.GetAsync(commentId).Returns(comment);
        _likeOperations.LikeAsync(comment, EventType.LikedTopicComment).Returns(expectedUser);

        var result = await _service.LikeCommentAsync(commentId);

        result.Should().Be(expectedUser);
        await _likeOperations.Received(1).LikeAsync(comment, EventType.LikedTopicComment);
    }

    [Fact]
    public async Task ThrowWhenLikingCommentByBlockedUser()
    {
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Current.User.UserId;
        var comment = new Comment
        {
            Id = commentId,
            Author = new GeneralUser { UserId = authorId }
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _blacklistChecker.IsBlockedAsync(authorId, currentUserId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.LikeCommentAsync(commentId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("Вы не можете поставить лайк этому комментарию");
    }

    [Fact]
    public async Task UnlikeTopic()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        await _service.UnlikeTopicAsync(topicId);

        await _likeOperations.Received(1).UnlikeAsync(topic);
    }

    [Fact]
    public async Task UnlikeComment()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        _commentService.GetAsync(commentId).Returns(comment);

        await _service.UnlikeCommentAsync(commentId);

        await _likeOperations.Received(1).UnlikeAsync(comment);
    }
}
