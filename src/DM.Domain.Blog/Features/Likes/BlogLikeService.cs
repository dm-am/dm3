using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Likes;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified blog like service implementation
/// </summary>
/// <remarks>
/// The blog's blacklist is not asked: a like is feedback, and the list closes
/// comments only (BlogRoleExtensions.IsBlacklisted).
/// </remarks>
internal class BlogLikeService : IBlogLikeService
{
    private readonly IPublicationService _publicationService;
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;

    public BlogLikeService(
        IPublicationService publicationService,
        IBlogCommentService blogCommentService,
        IPublicationCommentService publicationCommentService,
        IIntentionManager intentionManager,
        ILikeOperations likeOperations)
    {
        _publicationService = publicationService;
        _blogCommentService = blogCommentService;
        _publicationCommentService = publicationCommentService;
        _intentionManager = intentionManager;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeBlogCommentAsync(Guid commentId)
    {
        var comment = await LikeableBlogComment(commentId);
        return await _likeOperations.LikeAsync(comment, EventType.LikedBlogComment);
    }

    /// <inheritdoc />
    public async Task UnlikeBlogCommentAsync(Guid commentId)
    {
        var comment = await LikeableBlogComment(commentId);
        await _likeOperations.UnlikeAsync(comment);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikePublicationCommentAsync(Guid commentId)
    {
        var comment = await LikeablePublicationComment(commentId);
        return await _likeOperations.LikeAsync(comment, EventType.LikedPublicationComment);
    }

    /// <inheritdoc />
    public async Task UnlikePublicationCommentAsync(Guid commentId)
    {
        var comment = await LikeablePublicationComment(commentId);
        await _likeOperations.UnlikeAsync(comment);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikePublicationAsync(Guid publicationId)
    {
        var publication = await LikeablePublication(publicationId);
        return await _likeOperations.LikeAsync(publication, EventType.LikedPublication);
    }

    /// <inheritdoc />
    public async Task UnlikePublicationAsync(Guid publicationId)
    {
        var publication = await LikeablePublication(publicationId);
        await _likeOperations.UnlikeAsync(publication);
    }

    /// <summary>
    /// The subject of a like, with the caller's right to move one on it already
    /// checked.
    /// </summary>
    private async Task<Comment> LikeableBlogComment(Guid commentId)
    {
        var comment = await _blogCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return comment;
    }

    /// <inheritdoc cref="LikeableBlogComment" />
    private async Task<Comment> LikeablePublicationComment(Guid commentId)
    {
        var comment = await _publicationCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);
        return comment;
    }

    /// <inheritdoc cref="LikeableBlogComment" />
    private async Task<Publication> LikeablePublication(Guid publicationId)
    {
        var publication = await _publicationService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);
        return publication;
    }
}
