using System;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.RoomAccesses;

public interface IRoomAccessService
{
    Task<RoomAccess> CreateAsync(CreateRoomAccess createRoomAccess);

    Task<RoomAccess> GetAsync(Guid accessId);

    Task<RoomAccess> UpdateAsync(UpdateRoomAccess updateRoomAccess);

    Task DeleteAsync(Guid accessId);
}
