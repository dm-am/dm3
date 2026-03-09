using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Likes;

/// <summary>
/// Unified API service for all blog-related likes
/// </summary>
public interface IBlogLikeApiService
{
    /// <summary>
    /// Like a blog comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who just liked the comment</returns>
    Task<User> LikeBlogComment(Guid commentId);

    /// <summary>
    /// Remove user's like from blog comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task UnlikeBlogComment(Guid commentId);

    /// <summary>
    /// Like a publication comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope for user who just liked the comment</returns>
    Task<Envelope<User>> LikePublicationComment(Guid commentId);

    /// <summary>
    /// Remove user's like from publication comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    Task UnlikePublicationComment(Guid commentId);

    /// <summary>
    /// Like a publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns>Envelope for user who just liked the publication</returns>
    Task<Envelope<User>> LikePublication(Guid publicationId);

    /// <summary>
    /// Remove user's like from publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    Task UnlikePublication(Guid publicationId);
}
