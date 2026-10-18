using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;

namespace DM.Domain.Forum.Features.Likes;

/// <summary>
/// Forum like service
/// </summary>
internal class TopicLikeService : ITopicLikeService
{
    private readonly ITopicService _topicService;
    private readonly ITopicCommentService _commentService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;

    public TopicLikeService(
        ITopicService topicService,
        ITopicCommentService commentService,
        IIntentionManager intentionManager,
        ILikeOperations likeOperations)
    {
        _topicService = topicService;
        _commentService = commentService;
        _intentionManager = intentionManager;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The forum is a public place, and the personal blacklist of the topic's
    /// author does not close it: keeping somebody out is a ban, asked by the
    /// gate. The author still hears nothing about a blocked person's like -
    /// the notification leaves out whoever blocked its actor (NotificationService).
    /// </remarks>
    public async Task<GeneralUser> LikeTopicAsync(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        _intentionManager.ThrowIfForbidden(TopicIntention.Like, topic);
        return await _likeOperations.LikeAsync(topic, EventType.LikedTopic);
    }

    /// <inheritdoc />
    /// <remarks>The same as a like on the topic.</remarks>
    public async Task<GeneralUser> LikeCommentAsync(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return await _likeOperations.LikeAsync(comment, EventType.LikedTopicComment);
    }

    /// <inheritdoc />
    public async Task UnlikeTopicAsync(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        _intentionManager.ThrowIfForbidden(TopicIntention.Like, topic);
        await _likeOperations.UnlikeAsync(topic);
    }

    /// <inheritdoc />
    public async Task UnlikeCommentAsync(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        await _likeOperations.UnlikeAsync(comment);
    }
}
