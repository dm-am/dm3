using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Deleting;

/// <summary>
/// Storage for room accesses deleting
/// </summary>
internal interface IRoomAccessDeletingRepository
{
    /// <summary>
    /// Delete existing link
    /// </summary>
    /// <param name="deleteLink"></param>
    /// <returns></returns>
    Task Delete(IUpdateBuilder<RoomAccess> deleteLink);
}