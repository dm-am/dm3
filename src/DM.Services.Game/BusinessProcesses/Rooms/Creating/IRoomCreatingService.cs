using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Rooms.Creating;

/// <summary>
/// Creating service for rooms
/// </summary>
public interface IRoomCreatingService
{
    /// <summary>
    /// Create new room
    /// </summary>
    /// <param name="createRoom">DTO for room creating</param>
    /// <returns></returns>
    Task<Room> Create(CreateRoom createRoom);
}