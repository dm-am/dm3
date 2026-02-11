using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

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

    /// <inheritdoc />
    public RoomAccess Create(CreateRoomAccess roomAccess, Guid participantId)
    {
        return new RoomAccess
        {
            AccessId = _guidFactory.Create(),
            Policy = roomAccess.Policy,
            RoomId = roomAccess.RoomId,
            ParticipantId = participantId
        };
    }
}