using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.RoomAccesses;

internal interface IReaderClaimApprove
{
    /// <summary>
    /// The reader's user id, refused unless the game knows the name
    /// </summary>
    Task<Guid> GetReaderUserId(string readerUsername, RoomToUpdate room);
}
