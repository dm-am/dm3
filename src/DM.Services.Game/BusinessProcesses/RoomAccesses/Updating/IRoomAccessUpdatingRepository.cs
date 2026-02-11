using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using RoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Updating;

/// <summary>
/// Storage for room accesses updating
/// </summary>
internal interface IRoomAccessUpdatingRepository
{
    /// <summary>
    /// Update room access
    /// </summary>
    /// <param name="updateAccess">Update rules</param>
    /// <returns></returns>
    Task<Dto.Output.RoomAccess> Update(IUpdateBuilder<RoomAccess> updateAccess);
}