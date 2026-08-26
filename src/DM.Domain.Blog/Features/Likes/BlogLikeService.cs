using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Likes;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified blog like service implementation
/// </summary>
internal class BlogLikeService : IBlogLikeService
{
    private readonly IBlogService _blogService;
    private readonly IPublicationService _publicationService;
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;

    public BlogLikeService(
        IBlogService blogService,
        IPublicationService publicationService,
        IBlogCommentService blogCommentService,
        IPublicationCommentService publicationCommentService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeOperations likeOperations)
    {
        _blogService = blogService;
        _publicationService = publicationService;
        _blogCommentService = blogCommentService;
        _publicationCommentService = publicationCommentService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
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
    /// <remarks>
    /// The blacklist closes writing, and both putting a like on something and
    /// taking one back are writing - a reader shut out of a blog does not get to
    /// change what it shows either way. Which blog answers for the subject is the
    /// only thing that differs between the three: a blog comment names it
    /// directly, a publication and its comments through the publication.
    /// </remarks>
    private async Task<Comment> LikeableBlogComment(Guid commentId)
    {
        var comment = await _blogCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // EntityId is BlogId for blog comments
        ThrowIfBlacklisted(await _blogService.GetBlogAsync(comment.EntityId));
        return comment;
    }

    /// <inheritdoc cref="LikeableBlogComment" />
    private async Task<Comment> LikeablePublicationComment(Guid commentId)
    {
        var comment = await _publicationCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // EntityId is PublicationId for publication comments
        var publication = await _publicationService.GetPublication(comment.EntityId);
        ThrowIfBlacklisted(await _blogService.GetBlogAsync(publication.BlogId));
        return comment;
    }

    /// <inheritdoc cref="LikeableBlogComment" />
    private async Task<Publication> LikeablePublication(Guid publicationId)
    {
        var publication = await _publicationService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);

        ThrowIfBlacklisted(await _blogService.GetBlogAsync(publication.BlogId));
        return publication;
    }

    private void ThrowIfBlacklisted(BlogDto blog)
    {
        if (blog.BlacklistedUserIds.Contains(_identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromBlog);
        }
    }
}
