using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.Rooms.Creating;

/// <inheritdoc />
internal class RoomFactory : IRoomFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public RoomFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Room CreateFirst(CreateRoom createRoom)
    {
        return new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = createRoom.GameId,
            Title = createRoom.Title.Trim(),
            Type = createRoom.Type,
            AccessType = createRoom.AccessType,
            OrderNumber = 0,
            ViewPrivateText = createRoom.ViewPrivateText,
            ViewDiceResults = createRoom.ViewDiceResults,
            DiceEnabled = createRoom.DiceEnabled
        };
    }

    /// <inheritdoc />
    public Room CreateAfter(CreateRoom createRoom, Guid lastRoomId, double lastRoomOrderNumber)
    {
        var result = CreateFirst(createRoom);
        result.PreviousRoomId = lastRoomId;
        result.OrderNumber = lastRoomOrderNumber + 1;
        return result;
    }
}