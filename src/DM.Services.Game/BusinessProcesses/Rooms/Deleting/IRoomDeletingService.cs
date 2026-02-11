using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.Rooms.Deleting;

/// <summary>
/// Service for room deleting
/// </summary>
public interface IRoomDeletingService
{
    /// <summary>
    /// Delete existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <returns></returns>
    Task Delete(Guid roomId);
}