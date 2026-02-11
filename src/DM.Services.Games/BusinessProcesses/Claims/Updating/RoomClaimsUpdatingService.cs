using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Claims.Reading;
using DM.Services.Gaming.BusinessProcesses.Rooms.Updating;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using FluentValidation;
using DbRoomClaim = DM.Services.DataAccess.BusinessObjects.Games.Links.RoomClaim;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Updating;

/// <inheritdoc />
internal class RoomClaimsUpdatingService : IRoomClaimsUpdatingService
{
    private readonly IValidator<UpdateRoomClaim> _validator;
    private readonly IRoomClaimsUpdatingRepository _repository;
    private readonly IRoomClaimsReadingRepository _readingRepository;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomClaimsUpdatingService(
        IValidator<UpdateRoomClaim> validator,
        IRoomClaimsUpdatingRepository repository,
        IRoomClaimsReadingRepository readingRepository,
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
    public async Task<RoomClaim> Update(UpdateRoomClaim updateRoomClaim)
    {
        await _validator.ValidateAndThrowAsync(updateRoomClaim);
        var currentUserId = _identityProvider.Current.User.UserId;
        var oldClaim = await _readingRepository.GetClaim(updateRoomClaim.ClaimId, currentUserId);
        var room = await _roomUpdatingRepository.GetRoom(oldClaim.RoomId, currentUserId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        if (oldClaim.User != null && updateRoomClaim.Policy == RoomAccessPolicy.Full)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(RoomClaim.Policy)] = ValidationError.Invalid
            });
        }

        var updateBuilder = _updateBuilderFactory.Create<DbRoomClaim>(updateRoomClaim.ClaimId)
            .Field(c => c.Policy, updateRoomClaim.Policy);
        return await _repository.Update(updateBuilder);
    }
}