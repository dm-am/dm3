using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Claims.Reading;
using DM.Services.Gaming.BusinessProcesses.Rooms.Updating;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Deleting;

/// <inheritdoc />
internal class RoomClaimsDeletingService : IRoomClaimsDeletingService
{
    private readonly IRoomClaimsDeletingRepository _repository;
    private readonly IRoomClaimsReadingRepository _readingRepository;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomClaimsDeletingService(
        IRoomClaimsDeletingRepository repository,
        IRoomClaimsReadingRepository readingRepository,
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
    public async Task Delete(Guid claimId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var oldClaim = await _readingRepository.GetClaim(claimId, currentUserId);
        var room = await _roomUpdatingRepository.GetRoom(oldClaim.RoomId, currentUserId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var updateBuilder = _updateBuilderFactory.Create<RoomClaim>(claimId).Delete();
        await _repository.Delete(updateBuilder);
        await _producer.Send(EventType.ChangedRoom, room.Id);
    }
}