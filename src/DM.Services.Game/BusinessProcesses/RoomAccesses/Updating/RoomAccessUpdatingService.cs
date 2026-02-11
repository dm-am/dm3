using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.RelationalStorage;
using HttpException = DM.Services.Core.Exceptions.HttpException;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.RoomAccesses.Reading;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using FluentValidation;
using DbRoomAccess = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomAccess;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Updating;

/// <inheritdoc />
internal class RoomAccessUpdatingService : IRoomAccessUpdatingService
{
    private readonly IValidator<UpdateRoomAccess> _validator;
    private readonly IRoomAccessUpdatingRepository _repository;
    private readonly IRoomAccessReadingRepository _readingRepository;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomAccessUpdatingService(
        IValidator<UpdateRoomAccess> validator,
        IRoomAccessUpdatingRepository repository,
        IRoomAccessReadingRepository readingRepository,
        IRoomUpdatingRepository roomUpdatingRepository,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _repository = repository;
        _readingRepository = readingRepository;
        _roomUpdatingRepository = roomUpdatingRepository;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<RoomAccess> Update(UpdateRoomAccess updateRoomAccess)
    {
        await _validator.ValidateAndThrowAsync(updateRoomAccess);
        var currentUserId = _identityProvider.Current.User.UserId;
        var oldAccess = await _readingRepository.GetAccess(updateRoomAccess.AccessId, currentUserId);
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

        if (oldAccess.User != null && updateRoomAccess.Policy == RoomAccessPolicy.Full)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["Policy"] = ValidationError.Invalid
            });
        }

        var updateBuilder = _updateBuilderFactory.Create<DbRoomAccess>(updateRoomAccess.AccessId)
            .Field(c => c.Policy, updateRoomAccess.Policy);
        return await _repository.Update(updateBuilder);
    }
}