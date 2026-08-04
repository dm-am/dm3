using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// API service for room accesses
/// </summary>
public interface IRoomAccessApiService
{
    /// <summary>
    /// Create new room access
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="access">Access</param>
    /// <returns>Envelope containing the created room access</returns>
    Task<Envelope<RoomAccess>> Create(Guid roomId, RoomAccess access);

    /// <summary>
    /// Update existing room access
    /// </summary>
    /// <param name="accessId">Access identifier</param>
    /// <param name="request">Editable access fields</param>
    /// <returns>Envelope containing the updated room access</returns>
    Task<Envelope<RoomAccess>> Update(Guid accessId, UpdateRoomAccessRequest request);

    /// <summary>
    /// Delete existing room access
    /// </summary>
    /// <param name="accessId"></param>
    Task Delete(Guid accessId);
}
