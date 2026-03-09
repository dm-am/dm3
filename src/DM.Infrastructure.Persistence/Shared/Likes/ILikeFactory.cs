using System;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.CrossDomain;

namespace DM.Infrastructure.Persistence.Shared.Likes;

/// <summary>
/// Creates like DAL models
/// </summary>
internal interface ILikeFactory
{
    /// <summary>
    /// Create DAL model to store
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <param name="entityType">Entity type</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Like DAL model</returns>
    Like Create(Guid entityId, LikeEntityType entityType, Guid userId);
}
