using System;
using System.Threading.Tasks;
using DM.Services.Game.Dto.Internal;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <summary>
/// Validate and decide reader participant in room access
/// </summary>
internal interface IReaderClaimApprove
{
    /// <summary>
    /// Decide reader participant identifier
    /// </summary>
    /// <param name="readerLogin">Reader login</param>
    /// <param name="room">Room to update</param>
    /// <returns></returns>
    Task<Guid> GetParticipantId(string readerLogin, RoomToUpdate room);
}