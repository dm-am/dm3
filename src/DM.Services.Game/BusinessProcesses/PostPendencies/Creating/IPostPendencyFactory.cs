using System;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <summary>
/// Factory for post pendency DAL model
/// </summary>
internal interface IPostPendencyFactory
{
    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="createPostPendency">DTO model</param>
    /// <param name="createdById">User who created the pendency</param>
    /// <param name="waitingForUserId">User who is expected to post</param>
    /// <returns></returns>
    DataAccess.BusinessObjects.Games.Links.PostPendency Create(CreatePostPendency createPostPendency, Guid createdById, Guid waitingForUserId);
}
