using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// API service for game rooms
/// </summary>
public interface IRoomApiService
{
    /// <summary>
    /// Get list of all available game rooms
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>List envelope containing all game rooms</returns>
    Task<ListEnvelope<Room>> GetAll(Guid gameId);

    /// <summary>
    /// Get single room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <returns>Envelope containing the room</returns>
    Task<Envelope<Room>> Get(Guid roomId);

    /// <summary>
    /// Create new room
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="room">Room creation request</param>
    /// <returns>Envelope containing the created room</returns>
    Task<Envelope<Room>> Create(Guid gameId, CreateRoomRequest room);

    /// <summary>
    /// Update existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="request">Editable room fields</param>
    /// <returns>Envelope containing the updated room</returns>
    Task<Envelope<Room>> Update(Guid roomId, UpdateRoomRequest request);

    /// <summary>
    /// Delete existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    Task Delete(Guid roomId);
}
