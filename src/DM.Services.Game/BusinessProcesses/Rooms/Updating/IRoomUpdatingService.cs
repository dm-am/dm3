using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Rooms.Updating;

/// <summary>
/// Updating service for rooms
/// </summary>
public interface IRoomUpdatingService
{
    /// <summary>
    /// Update existing room
    /// </summary>
    /// <param name="updateRoom">DTO for room updating</param>
    /// <returns></returns>
    Task<Room> Update(UpdateRoom updateRoom);
}