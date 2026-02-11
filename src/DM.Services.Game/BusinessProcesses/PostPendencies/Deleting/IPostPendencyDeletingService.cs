using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Deleting;

/// <summary>
/// Service for post pendency deleting
/// </summary>
public interface IPostPendencyDeletingService
{
    /// <summary>
    /// Delete existing post pendency
    /// </summary>
    /// <param name="pendencyId">Post pendency identifier</param>
    /// <returns></returns>
    Task Delete(Guid pendencyId);
}
