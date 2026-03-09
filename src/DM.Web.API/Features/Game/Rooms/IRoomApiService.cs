using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
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
    /// <returns></returns>
    Task<ListEnvelope<Room>> GetAll(Guid gameId);

    /// <summary>
    /// Get list of game rooms filtered by type
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="roomType">Room type filter</param>
    /// <returns></returns>
    Task<ListEnvelope<Room>> GetByType(Guid gameId, RoomType roomType);

    /// <summary>
    /// Get single room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <returns></returns>
    Task<Envelope<Room>> Get(Guid roomId);

    /// <summary>
    /// Create new room
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="room">Room creation request</param>
    /// <returns></returns>
    Task<Envelope<Room>> Create(Guid gameId, CreateRoomRequest room);

    /// <summary>
    /// Update existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <param name="room">Room model</param>
    /// <returns></returns>
    Task<Envelope<Room>> Update(Guid roomId, Room room);

    /// <summary>
    /// Delete existing room
    /// </summary>
    /// <param name="roomId">Room identifier</param>
    /// <returns></returns>
    Task Delete(Guid roomId);
}
