using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Deleting;

/// <inheritdoc />
internal class RoomAccessDeletingService : IRoomAccessDeletingService
{
    private readonly IRoomAccessDeletingRepository _repository;
    private readonly IRoomAccessReadingRepository _readingRepository;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomAccessDeletingService(
        IRoomAccessDeletingRepository repository,
        IRoomAccessReadingRepository readingRepository,
        IRoomUpdatingRepository roomUpdatingRepository,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _readingRepository = readingRepository;
        _roomUpdatingRepository = roomUpdatingRepository;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task Delete(Guid accessId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var oldAccess = await _readingRepository.GetAccess(accessId, currentUserId);
        if (oldAccess == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Room access not found");
        }

        var room = await _roomUpdatingRepository.GetRoom(oldAccess.RoomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var updateBuilder = _updateBuilderFactory.Create<RoomAccess>(accessId).Delete();
        await _repository.Delete(updateBuilder);
        await _producer.Send(EventType.ChangedRoom, room.Id);
    }
}