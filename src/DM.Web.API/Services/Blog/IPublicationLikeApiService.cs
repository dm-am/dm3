using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Blog;

/// <summary>
/// API service for publication and comment likes
/// </summary>
public interface IPublicationLikeApiService
{
    /// <summary>
    /// Like the publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns>Envelope for user who just liked the publication</returns>
    Task<Envelope<User>> LikePublication(Guid publicationId);

    /// <summary>
    /// Remove user's like from publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns></returns>
    Task DislikePublication(Guid publicationId);

    /// <summary>
    /// Like the comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Envelope for user who just liked the comment</returns>
    Task<Envelope<User>> LikeComment(Guid commentId);

    /// <summary>
    /// Remove user's like from comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task DislikeComment(Guid commentId);
}
