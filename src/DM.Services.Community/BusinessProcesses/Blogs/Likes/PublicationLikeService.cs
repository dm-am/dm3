using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Likes;
using DM.Services.Community.BusinessProcesses.Blogs.Comments;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Blogs.Likes;

/// <summary>
/// Publication and comment like service
/// </summary>
internal class PublicationLikeService : LikeServiceBase, IPublicationLikeService
{
    private readonly IBlogService _blogService;
    private readonly ICommentReadingService _commentReadingService;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public PublicationLikeService(
        IBlogService blogService,
        ICommentReadingService commentReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeFactory likeFactory,
        ILikeRepository likeRepository,
        IInvokedEventProducer invokedEventProducer)
        : base(identityProvider, likeFactory, likeRepository, invokedEventProducer)
    {
        _blogService = blogService;
        _commentReadingService = commentReadingService;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikePublication(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);
        return await Like(publication, EventType.LikedPublication);
    }

    /// <inheritdoc />
    public async Task DislikePublication(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);
        await Dislike(publication);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeComment(Guid commentId)
    {
        var comment = await _commentReadingService.Get(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return await Like(comment, EventType.LikedBlogComment);
    }

    /// <inheritdoc />
    public async Task DislikeComment(Guid commentId)
    {
        var comment = await _commentReadingService.Get(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        await Dislike(comment);
    }
}
