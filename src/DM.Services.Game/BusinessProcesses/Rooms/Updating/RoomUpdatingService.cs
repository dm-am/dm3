using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Internal;
using DM.Services.Game.Dto.Output;
using FluentValidation;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Updating;

/// <inheritdoc />
internal class RoomUpdatingService : IRoomUpdatingService
{
    private readonly IValidator<UpdateRoom> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IRoomOrderPull _roomOrderPull;
    private readonly IRoomUpdatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomUpdatingService(
        IValidator<UpdateRoom> validator,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IRoomOrderPull roomOrderPull,
        IRoomUpdatingRepository repository,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _roomOrderPull = roomOrderPull;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Room> Update(UpdateRoom updateRoom)
    {
        await _validator.ValidateAndThrowAsync(updateRoom);
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _repository.GetRoom(updateRoom.RoomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var roomUpdate = _updateBuilderFactory.Create<DbRoom>(updateRoom.RoomId)
            .MaybeField(r => r.Title, updateRoom.Title)
            .MaybeField(r => r.Type, updateRoom.Type)
            .MaybeField(r => r.AccessType, updateRoom.AccessType)
            .MaybeField(r => r.ViewPrivateText, updateRoom.ViewPrivateText)
            .MaybeField(r => r.ViewDiceResults, updateRoom.ViewDiceResults)
            .MaybeField(r => r.DiceEnabled, updateRoom.DiceEnabled);

        if (updateRoom.PreviousRoomId == null || updateRoom.PreviousRoomId.Value == room.PreviousRoomId)
        {
            return await _repository.Update(roomUpdate);
        }

        if (!updateRoom.PreviousRoomId.Value.HasValue)
        {
            return await InsertFirst(room, roomUpdate);
        }

        var targetRoom = await _repository.GetRoom(updateRoom.PreviousRoomId.Value.Value, currentUserId);
        if (targetRoom == null || targetRoom.Game.Id != room.Game.Id)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(Room.PreviousRoomId)] = ValidationError.Invalid
            });
        }

        return await InsertAfter(room, targetRoom, roomUpdate);
    }

    private async Task<Room> InsertAfter(RoomToUpdate room, RoomToUpdate afterRoom,
        IUpdateBuilder<DbRoom> updateRoom)
    {
        var (updateOldPreviousRoom, updateOldNextRoom) = _roomOrderPull.GetPullChanges(room);
        var updateNewPreviousRoom = _updateBuilderFactory.Create<DbRoom>(afterRoom.Id)
            .Field(r => r.NextRoomId, room.Id);
        var updateNewNextRoom = afterRoom.NextRoom == null
            ? null
            : _updateBuilderFactory.Create<DbRoom>(afterRoom.NextRoom.Id)
                .Field(r => r.PreviousRoomId, room.Id);

        var nextRoomOrderNumber = afterRoom.NextRoom?.OrderNumber;
        updateRoom
            .Field(r => r.PreviousRoomId, afterRoom.Id)
            .Field(r => r.NextRoomId, afterRoom.NextRoom?.Id)
            .Field(r => r.OrderNumber, nextRoomOrderNumber.HasValue
                ? (nextRoomOrderNumber.Value + afterRoom.OrderNumber) / 2
                : afterRoom.OrderNumber + 1);

        return await _repository.Update(updateRoom,
            updateOldNextRoom, updateOldPreviousRoom, updateNewNextRoom, updateNewPreviousRoom);
    }

    private async Task<Room> InsertFirst(RoomToUpdate room, IUpdateBuilder<DbRoom> updateRoom)
    {
        var (updateOldPreviousRoom, updateOldNextRoom) = _roomOrderPull.GetPullChanges(room);
        var firstRoomInfo = await _repository.GetFirstRoomInfo(room.Game.Id);
        if (firstRoomInfo == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "First room not found");
        }
        var updateNewNextRoom = _updateBuilderFactory.Create<DbRoom>(firstRoomInfo.Id)
            .Field(r => r.PreviousRoomId, room.Id);
        updateRoom
            .Field(r => r.NextRoomId, firstRoomInfo.Id)
            .Field(r => r.OrderNumber, firstRoomInfo.OrderNumber - 1);

        return await _repository.Update(updateRoom, updateOldNextRoom, updateOldPreviousRoom, updateNewNextRoom);
    }
}