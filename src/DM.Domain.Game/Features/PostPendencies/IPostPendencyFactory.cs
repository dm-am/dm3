using System;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.PostPendencies;
/// <summary>
/// Factory for post pendency entity DTO
/// </summary>
internal interface IPostPendencyFactory
{
    /// <summary>
    /// Create new post pendency entity DTO
    /// </summary>
    /// <param name="createPostPendency">Input DTO</param>
    /// <param name="createdById">User who created the pendency</param>
    /// <param name="waitingForUserId">User who is expected to post</param>
    CreatePostPendencyEntity Create(CreatePostPendency createPostPendency, Guid createdById, Guid waitingForUserId);
}
