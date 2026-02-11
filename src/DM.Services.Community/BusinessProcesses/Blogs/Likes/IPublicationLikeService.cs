using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Likes;

/// <summary>
/// Service for publication and comment likes
/// </summary>
public interface IPublicationLikeService
{
    /// <summary>
    /// Create new like from current user to selected publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns>User who liked the publication</returns>
    Task<GeneralUser> LikePublication(Guid publicationId);

    /// <summary>
    /// Remove existing like from current user to selected publication
    /// </summary>
    /// <param name="publicationId">Publication identifier</param>
    /// <returns></returns>
    Task DislikePublication(Guid publicationId);

    /// <summary>
    /// Create new like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>User who liked the comment</returns>
    Task<GeneralUser> LikeComment(Guid commentId);

    /// <summary>
    /// Remove existing like from current user to selected comment
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task DislikeComment(Guid commentId);
}
