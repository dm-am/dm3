using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Likes;
using DM.Domain.Game.Features.Comments;

namespace DM.Domain.Game.Features.Likes;

/// <summary>
/// Game comment like service
/// </summary>
/// <remarks>
/// The game's blacklist is not asked: a like is feedback, and the list closes
/// the discussion and the application only (GameRoleExtensions.IsBlacklisted).
/// </remarks>
internal class GameCommentLikeService : IGameCommentLikeService
{
    private readonly IGameCommentService _commentService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;

    public GameCommentLikeService(
        IGameCommentService commentService,
        IIntentionManager intentionManager,
        ILikeOperations likeOperations)
    {
        _commentService = commentService;
        _intentionManager = intentionManager;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeCommentAsync(Guid commentId)
    {
        var comment = await LikeableComment(commentId);
        return await _likeOperations.LikeAsync(comment, EventType.LikedGameComment);
    }

    /// <inheritdoc />
    public async Task UnlikeCommentAsync(Guid commentId)
    {
        var comment = await LikeableComment(commentId);
        await _likeOperations.UnlikeAsync(comment);
    }

    /// <summary>
    /// The comment, with the caller's right to move a like on it already checked.
    /// </summary>
    private async Task<Comment> LikeableComment(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return comment;
    }
}
