using System;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Shared;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <summary>
/// Storage for topic likes
/// </summary>
internal interface ILikeRepository
{
    /// <summary>
    /// Store new like
    /// </summary>
    /// <param name="like">Like DAL model</param>
    Task Add(Like like);

    /// <summary>
    /// Whether this reader has a standing like on this entity
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="userId">User identifier</param>
    /// <remarks>
    /// Asked of the store rather than of the entity that was just read: the
    /// mapping profiles of the topic and the blog declare Likes as Ignore, so
    /// the collection arrives empty however many likes the row has, and a
    /// question put to it answers about nothing.
    /// </remarks>
    Task<bool> Exists(Guid entityId, Guid userId);

    /// <summary>
    /// Delete like from storage
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="userId">User identifier</param>
    Task Delete(Guid entityId, Guid userId);
}
