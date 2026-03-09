using System;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.CrossDomain;

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
    /// <returns></returns>
    Task Add(Like like);

    /// <summary>
    /// Delete like from storage
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns></returns>
    Task Delete(Guid entityId, Guid userId);
}
