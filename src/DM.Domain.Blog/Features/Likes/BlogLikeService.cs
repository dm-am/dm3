using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Likes;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified blog like service implementation
/// </summary>
internal class BlogLikeService : IBlogLikeService
{
    private readonly IBlogService _blogService;
    private readonly IBlogCommentService _blogCommentService;
    private readonly IPublicationCommentService _publicationCommentService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;

    public BlogLikeService(
        IBlogService blogService,
        IBlogCommentService blogCommentService,
        IPublicationCommentService publicationCommentService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeOperations likeOperations)
    {
        _blogService = blogService;
        _blogCommentService = blogCommentService;
        _publicationCommentService = publicationCommentService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeBlogCommentAsync(Guid commentId)
    {
        var comment = await _blogCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // EntityId is BlogId for blog comments
        var blog = await _blogService.GetBlogAsync(comment.EntityId);
        ThrowIfBlacklisted(blog);

        return await _likeOperations.LikeAsync(comment, EventType.LikedBlogComment);
    }

    /// <inheritdoc />
    public async Task UnlikeBlogCommentAsync(Guid commentId)
    {
        var comment = await _blogCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        var blog = await _blogService.GetBlogAsync(comment.EntityId);
        ThrowIfBlacklisted(blog);

        await _likeOperations.UnlikeAsync(comment);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikePublicationCommentAsync(Guid commentId)
    {
        var comment = await _publicationCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        // EntityId is PublicationId for publication comments
        var publication = await _blogService.GetPublication(comment.EntityId);
        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        ThrowIfBlacklisted(blog);

        return await _likeOperations.LikeAsync(comment, EventType.LikedPublicationComment);
    }

    /// <inheritdoc />
    public async Task UnlikePublicationCommentAsync(Guid commentId)
    {
        var comment = await _publicationCommentService.GetAsync(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Like, comment);

        var publication = await _blogService.GetPublication(comment.EntityId);
        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        ThrowIfBlacklisted(blog);

        await _likeOperations.UnlikeAsync(comment);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikePublicationAsync(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);

        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        ThrowIfBlacklisted(blog);

        return await _likeOperations.LikeAsync(publication, EventType.LikedPublication);
    }

    /// <inheritdoc />
    public async Task UnlikePublicationAsync(Guid publicationId)
    {
        var publication = await _blogService.GetPublication(publicationId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.Like, publication);

        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        ThrowIfBlacklisted(blog);

        await _likeOperations.UnlikeAsync(publication);
    }

    private void ThrowIfBlacklisted(BlogDto blog)
    {
        if (blog.BlacklistedUserIds.Contains(_identityProvider.Current.User.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are blacklisted from this blog");
        }
    }
}
