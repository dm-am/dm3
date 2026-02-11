using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Updating;

/// <summary>
/// Service for room accesses updating
/// </summary>
public interface IRoomAccessUpdatingService
{
    /// <summary>
    /// Update existing room access
    /// </summary>
    /// <param name="updateRoomAccess">DTO for update</param>
    /// <returns></returns>
    Task<RoomAccess> Update(UpdateRoomAccess updateRoomAccess);
}