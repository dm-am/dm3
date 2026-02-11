using System;
using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using DbPostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Deleting;

/// <summary>
/// Storage for post pendency deleting
/// </summary>
internal interface IPostPendencyDeletingRepository
{
    /// <summary>
    /// Get post pendency
    /// </summary>
    /// <param name="pendencyId">Identifier</param>
    /// <returns></returns>
    Task<PostPendency?> Get(Guid pendencyId);

    /// <summary>
    /// Delete post pendency
    /// </summary>
    /// <param name="updateBuilder"></param>
    /// <returns></returns>
    Task Delete(IUpdateBuilder<DbPostPendency> updateBuilder);
}
