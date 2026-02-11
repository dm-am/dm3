using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Internal;

namespace DM.Services.Game.BusinessProcesses.Rooms.Updating;

/// <inheritdoc />
internal class RoomOrderPull : IRoomOrderPull
{
    private readonly IUpdateBuilderFactory _updateBuilderFactory;

    /// <inheritdoc />
    public RoomOrderPull(
        IUpdateBuilderFactory updateBuilderFactory)
    {
        _updateBuilderFactory = updateBuilderFactory;
    }

    /// <inheritdoc />
    public (IUpdateBuilder<Room>? updateOldPrevious, IUpdateBuilder<Room>? updateOldNext) GetPullChanges(
        RoomToUpdate room)
    {
        IUpdateBuilder<Room>? updateOldPreviousRoom = room.PreviousRoom == null
            ? null
            : _updateBuilderFactory.Create<Room>(room.PreviousRoom.Id)
                .Field(r => r.NextRoomId, room.NextRoom?.Id);
        IUpdateBuilder<Room>? updateOldNextRoom = room.NextRoom == null
            ? null
            : _updateBuilderFactory.Create<Room>(room.NextRoom.Id)
                .Field(r => r.PreviousRoomId, room.PreviousRoom?.Id);
        return (updateOldPreviousRoom, updateOldNextRoom);
    }
}