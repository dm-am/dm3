using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Likes;

/// <summary>
/// Unified service for all blog-related likes
/// </summary>
public interface IBlogLikeService
{
    /// <summary>
    /// Create new like from current user to selected blog comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeBlogCommentAsync(Guid commentId);

    /// <summary>
    /// Remove existing like from current user to selected blog comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task UnlikeBlogCommentAsync(Guid commentId);

    /// <summary>
    /// Create new like from current user to selected publication comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikePublicationCommentAsync(Guid commentId);

    /// <summary>
    /// Remove existing like from current user to selected publication comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task UnlikePublicationCommentAsync(Guid commentId);

    /// <summary>
    /// Create new like from current user to selected publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns>User who liked the publication</returns>
    Task<GeneralUser> LikePublicationAsync(Guid publicationId);

    /// <summary>
    /// Remove existing like from current user to selected publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    Task UnlikePublicationAsync(Guid publicationId);
}
