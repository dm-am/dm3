using DM.Domain.Game.Features.Games;
using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;
/// <summary>
/// Validate and decide reader user in room access
/// </summary>
internal interface IReaderClaimApprove
{
    /// <summary>
    /// Validate and return reader user identifier
    /// </summary>
    /// <param name="readerUsername">Reader username</param>
    /// <param name="room">Room to update</param>
    /// <returns>Validated user ID</returns>
    Task<Guid> GetReaderUserId(string readerUsername, RoomToUpdate room);
}
