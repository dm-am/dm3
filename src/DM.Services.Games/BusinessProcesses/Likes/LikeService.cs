using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Likes;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Commentaries.Reading;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Gaming.BusinessProcesses.Likes;

/// <summary>
/// Forum like service
/// </summary>
internal class LikeService : LikeServiceBase, ILikeService
{
    private readonly ICommentaryReadingService _commentaryReadingService;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public LikeService(
        ICommentaryReadingService commentaryReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeFactory likeFactory,
        ILikeRepository likeRepository,
        IInvokedEventProducer invokedEventProducer)
        : base(identityProvider, likeFactory, likeRepository, invokedEventProducer)
    {
        _commentaryReadingService = commentaryReadingService;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeComment(Guid commentId)
    {
        var comment = await _commentaryReadingService.Get(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return await Like(comment, EventType.LikedGameComment);
    }

    /// <inheritdoc />
    public async Task DislikeComment(Guid commentId)
    {
        var comment = await _commentaryReadingService.Get(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        await Dislike(comment);
    }
}