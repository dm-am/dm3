using System;
using System.Threading.Tasks;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Deleting;

/// <summary>
/// Service for deleting room accesses
/// </summary>
public interface IRoomAccessDeletingService
{
    /// <summary>
    /// Delete existing access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    /// <returns></returns>
    Task Delete(Guid accessId);
}