using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Rooms;

public interface IRoomService
{
    Task<Room> CreateAsync(CreateRoom createRoom);

    Task<IEnumerable<Room>> GetAllAsync(Guid gameId);

    Task<Room> GetAsync(Guid roomId);

    /// <remarks>
    /// Every RoomIntention rule reads the roles of the game, which the plain projection does not carry.
    /// </remarks>
    Task<RoomToUpdate> GetWithGameAsync(Guid roomId);

    /// <summary>
    /// Resolving the room and deciding who may read it is one answer, given here
    /// </summary>
    Task<RoomToUpdate> GetChatRoomForReadingAsync(Guid roomId);

    /// <summary>
    /// The writing half of <see cref="GetChatRoomForReadingAsync"/>
    /// </summary>
    Task<RoomToUpdate> GetChatRoomForWritingAsync(Guid roomId);

    Task<Room> UpdateAsync(UpdateRoom updateRoom);

    Task DeleteAsync(Guid roomId);
}
