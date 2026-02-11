using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;

namespace DM.Services.Common.BusinessProcesses.Likes;

/// <summary>
/// Creates like DAL models
/// </summary>
public interface ILikeFactory
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