using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Rooms;

using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <summary>
/// Unified service for room access CRUD operations
/// </summary>
internal class RoomAccessService : IRoomAccessService
{
    private readonly IValidator<CreateRoomAccess> _createValidator;
    private readonly IValidator<UpdateRoomAccess> _updateValidator;
    private readonly IRoomRepository _roomRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomAccessFactory _factory;
    private readonly ICharacterClaimApprove _characterClaimApprove;
    private readonly IReaderClaimApprove _readerClaimApprove;
    private readonly IRoomAccessRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    public RoomAccessService(
        IValidator<CreateRoomAccess> createValidator,
        IValidator<UpdateRoomAccess> updateValidator,
        IRoomRepository roomRepository,
        IIntentionManager intentionManager,
        IRoomAccessFactory factory,
        ICharacterClaimApprove characterClaimApprove,
        IReaderClaimApprove readerClaimApprove,
        IRoomAccessRepository repository,
        IEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _roomRepository = roomRepository;
        _intentionManager = intentionManager;
        _factory = factory;
        _characterClaimApprove = characterClaimApprove;
        _readerClaimApprove = readerClaimApprove;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    #region Create

    public async Task<RoomAccess> CreateAsync(CreateRoomAccess createRoomAccess)
    {
        await _createValidator.ValidateAndThrowAsync(createRoomAccess);
        var room = await _roomRepository.GetForUpdate(createRoomAccess.RoomId, _identityProvider.Current.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var link = createRoomAccess.CharacterId.HasValue
            ? _factory.CreateForCharacter(createRoomAccess,
                await _characterClaimApprove.GetCharacterId(createRoomAccess.CharacterId.Value, room))
            : _factory.CreateForReader(createRoomAccess,
                await _readerClaimApprove.GetReaderUserId(createRoomAccess.ReaderUsername.Trim(), room));

        var result = await _repository.Create(link);
        await _producer.Send(EventType.ChangedRoom, link.RoomId);

        return result;
    }

    #endregion

    #region Read

    public Task<IEnumerable<RoomAccess>> GetGameAccessesAsync(Guid gameId) =>
        _repository.GetGameAccesses(gameId, _identityProvider.Current.User.UserId);

    public Task<IEnumerable<RoomAccess>> GetRoomAccessesAsync(Guid roomId) =>
        _repository.GetRoomAccesses(roomId, _identityProvider.Current.User.UserId);

    public async Task<RoomAccess> GetAsync(Guid accessId)
    {
        var access = await _repository.GetAccess(accessId, _identityProvider.Current.User.UserId);
        if (access == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Access not found");
        }
        return access;
    }

    #endregion

    #region Update

    public async Task<RoomAccess> UpdateAsync(UpdateRoomAccess updateRoomAccess)
    {
        await _updateValidator.ValidateAndThrowAsync(updateRoomAccess);

        var currentUserId = _identityProvider.Current.User.UserId;
        var oldAccess = await _repository.GetAccess(updateRoomAccess.AccessId, currentUserId);
        if (oldAccess == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room access not found");
        }

        var room = await _roomRepository.GetForUpdate(oldAccess.RoomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        if (oldAccess.User != null && updateRoomAccess.Policy == RoomAccessPolicy.Full)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["Policy"] = ValidationError.Invalid
            });
        }

        var updateEntity = new UpdateRoomAccessEntity
        {
            AccessId = updateRoomAccess.AccessId,
            Policy = updateRoomAccess.Policy
        };

        var result = await _repository.Update(updateEntity);
        await _producer.Send(EventType.ChangedRoom, oldAccess.RoomId);

        return result;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid accessId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var oldAccess = await _repository.GetAccess(accessId, currentUserId);
        if (oldAccess == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room access not found");
        }

        var room = await _roomRepository.GetForUpdate(oldAccess.RoomId, currentUserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        await _repository.Delete(accessId);
        await _producer.Send(EventType.ChangedRoom, room.Id);
    }

    #endregion
}
