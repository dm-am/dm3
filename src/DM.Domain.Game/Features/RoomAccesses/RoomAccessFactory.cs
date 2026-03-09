using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <inheritdoc />
internal class RoomAccessFactory : IRoomAccessFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public RoomAccessFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    public CreateRoomAccessEntity CreateForCharacter(CreateRoomAccess roomAccess, Guid characterId)
    {
        return new CreateRoomAccessEntity
        {
            AccessId = _guidFactory.Create(),
            Policy = roomAccess.Policy,
            RoomId = roomAccess.RoomId,
            CharacterId = characterId,
            ReaderUserId = null
        };
    }

    public CreateRoomAccessEntity CreateForReader(CreateRoomAccess roomAccess, Guid readerUserId)
    {
        return new CreateRoomAccessEntity
        {
            AccessId = _guidFactory.Create(),
            Policy = roomAccess.Policy,
            RoomId = roomAccess.RoomId,
            CharacterId = null,
            ReaderUserId = readerUserId
        };
    }
}
