using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Likes;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Likes;

/// <summary>
/// Game comment like service
/// </summary>
internal class GameCommentLikeService : IGameCommentLikeService
{
    private readonly IGameCommentService _commentService;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;

    public GameCommentLikeService(
        IGameCommentService commentService,
        IGameService gameService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeOperations likeOperations)
    {
        _commentService = commentService;
        _gameService = gameService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeCommentAsync(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // The blacklist closes writing, and a like is writing.
        var game = await _gameService.GetAsync(comment.EntityId);
        if (game.IsBlacklisted(_identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        return await _likeOperations.LikeAsync(comment, EventType.LikedGameComment);
    }

    /// <inheritdoc />
    public async Task UnlikeCommentAsync(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // The blacklist closes writing, and taking a like back is writing.
        var game = await _gameService.GetAsync(comment.EntityId);
        if (game.IsBlacklisted(_identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        await _likeOperations.UnlikeAsync(comment);
    }
}
