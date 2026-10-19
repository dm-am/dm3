using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Likes;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Likes;

public class TopicLikeServiceShould : UnitTestBase
{
    private readonly ITopicService topicService;
    private readonly ITopicCommentService commentService;
    private readonly IIntentionManager intentionManager;
    private readonly ILikeOperations likeOperations;
    private readonly TopicLikeService service;

    public TopicLikeServiceShould()
    {
        topicService = Mock<ITopicService>();
        commentService = Mock<ITopicCommentService>();

        intentionManager = Mock<IIntentionManager>();

        likeOperations = Mock<ILikeOperations>();
        likeOperations.LikeAsync(Arg.Any<Topic>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        likeOperations.LikeAsync(Arg.Any<Comment>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        likeOperations.UnlikeAsync(Arg.Any<Topic>()).Returns(Task.CompletedTask);
        likeOperations.UnlikeAsync(Arg.Any<Comment>()).Returns(Task.CompletedTask);

        service = new TopicLikeService(
            topicService,
            commentService,
            intentionManager,
            likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        await service.LikeTopicAsync(topicId);

        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Like, topic);
    }

    [Fact]
    public async Task LikeTopicAndPublishEvent()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        likeOperations.LikeAsync(topic, EventType.LikedTopic).Returns(expectedUser);

        var result = await service.LikeTopicAsync(topicId);

        result.Should().Be(expectedUser);
        await likeOperations.Received(1).LikeAsync(topic, EventType.LikedTopic);
    }

    [Fact]
    public async Task AuthorizeLikeCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        commentService.GetAsync(commentId).Returns(comment);

        await service.LikeCommentAsync(commentId);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task LikeCommentAndPublishEvent()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        var expectedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "User1" };
        commentService.GetAsync(commentId).Returns(comment);
        likeOperations.LikeAsync(comment, EventType.LikedTopicComment).Returns(expectedUser);

        var result = await service.LikeCommentAsync(commentId);

        result.Should().Be(expectedUser);
        await likeOperations.Received(1).LikeAsync(comment, EventType.LikedTopicComment);
    }

    [Fact]
    public async Task UnlikeTopic()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        await service.UnlikeTopicAsync(topicId);

        await likeOperations.Received(1).UnlikeAsync(topic);
    }

    [Fact]
    public async Task UnlikeComment()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId };
        commentService.GetAsync(commentId).Returns(comment);

        await service.UnlikeCommentAsync(commentId);

        await likeOperations.Received(1).UnlikeAsync(comment);
    }
}
