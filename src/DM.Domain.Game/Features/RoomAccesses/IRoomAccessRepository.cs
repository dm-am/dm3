using System;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.RoomAccesses;

public interface IRoomAccessRepository
{
    Task<RoomAccess?> GetAccess(Guid accessId, Guid userId);

    Task<Guid?> FindReaderUserId(Guid gameId, string readerUsername);

    Task<Guid?> FindCharacterGameId(Guid characterId);

    /// <summary>
    /// An access for the same room and target, character or reader
    /// </summary>
    Task<bool> AccessExists(Guid roomId, Guid? characterId, Guid? readerUserId);

    Task<RoomAccess> Create(CreateRoomAccessEntity entity);

    Task<RoomAccess> Update(UpdateRoomAccessEntity entity);

    Task Delete(Guid accessId);
}
