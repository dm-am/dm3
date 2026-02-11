using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <summary>
/// Service for room access creating
/// </summary>
public interface IRoomAccessCreatingService
{
    /// <summary>
    /// Create new room access
    /// </summary>
    /// <param name="createRoomAccess">DTO model</param>
    /// <returns></returns>
    Task<RoomAccess> Create(CreateRoomAccess createRoomAccess);
}