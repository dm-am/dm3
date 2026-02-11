using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Core.Exceptions;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.MessageQueuing.GeneralBus;
using DbRoom = DM.Services.DataAccess.BusinessObjects.Games.Posts.Room;

namespace DM.Services.Game.BusinessProcesses.Rooms.Deleting;

/// <inheritdoc />
internal class RoomDeletingService : IRoomDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IRoomOrderPull _roomOrderPull;
    private readonly IRoomUpdatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IRoomOrderPull roomOrderPull,
        IRoomUpdatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _roomOrderPull = roomOrderPull;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task Delete(Guid roomId)
    {
        var room = await _repository.GetRoom(roomId, _identityProvider.Current.User.UserId);
        if (room == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var updateRoom = _updateBuilderFactory.Create<DbRoom>(roomId).Field(r => r.IsRemoved, true);
        var (updateOldPreviousRoom, updateOldNextRoom) = _roomOrderPull.GetPullChanges(room);

        await _repository.Update(updateRoom, updateOldNextRoom, updateOldPreviousRoom);
        await _unreadCountersRepository.Delete(roomId, UnreadEntryType.Message);
        await _producer.Send(EventType.DeletedRoom, roomId);
    }
}