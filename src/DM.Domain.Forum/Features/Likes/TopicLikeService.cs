using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
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
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;

    public TopicLikeService(
        ITopicService topicService,
        ITopicCommentService commentService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeOperations likeOperations,
        IUserBlacklistChecker userBlacklistChecker)
    {
        _topicService = topicService;
        _commentService = commentService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _likeOperations = likeOperations;
        _userBlacklistChecker = userBlacklistChecker;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeTopicAsync(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        _intentionManager.ThrowIfForbidden(TopicIntention.Like, topic);

        // Check if topic author has blocked the current user
        var currentUserId = _identityProvider.Current.User.UserId;
        if (topic.Author != null && await _userBlacklistChecker.IsBlockedAsync(topic.Author.UserId, currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot like this topic");
        }

        return await _likeOperations.LikeAsync(topic, EventType.LikedTopic);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeCommentAsync(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // Check if comment author has blocked the current user
        var currentUserId = _identityProvider.Current.User.UserId;
        if (comment.Author != null && await _userBlacklistChecker.IsBlockedAsync(comment.Author.UserId, currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot like this comment");
        }

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
