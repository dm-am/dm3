using System;

namespace DM.Domain.Game.Features.RoomAccesses;

internal interface IRoomAccessFactory
{
    CreateRoomAccessEntity CreateForCharacter(CreateRoomAccess roomAccess, Guid characterId);

    CreateRoomAccessEntity CreateForReader(CreateRoomAccess roomAccess, Guid readerUserId);
}
