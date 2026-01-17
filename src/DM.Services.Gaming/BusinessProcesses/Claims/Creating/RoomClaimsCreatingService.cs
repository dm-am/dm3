using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Rooms.Updating;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Gaming.BusinessProcesses.Claims.Creating;

/// <inheritdoc />
internal class RoomClaimsCreatingService : IRoomClaimsCreatingService
{
    private readonly IValidator<CreateRoomClaim> _validator;
    private readonly IRoomUpdatingRepository _updatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomClaimFactory _factory;
    private readonly ICharacterClaimApprove _characterClaimApprove;
    private readonly IReaderClaimApprove _readerClaimApprove;
    private readonly IRoomClaimsCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomClaimsCreatingService(
        IValidator<CreateRoomClaim> validator,
        IRoomUpdatingRepository updatingRepository,
        IIntentionManager intentionManager,
        IRoomClaimFactory factory,
        ICharacterClaimApprove characterClaimApprove,
        IReaderClaimApprove readerClaimApprove,
        IRoomClaimsCreatingRepository repository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _updatingRepository = updatingRepository;
        _intentionManager = intentionManager;
        _factory = factory;
        _characterClaimApprove = characterClaimApprove;
        _readerClaimApprove = readerClaimApprove;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<RoomClaim> Create(CreateRoomClaim createRoomClaim)
    {
        await _validator.ValidateAndThrowAsync(createRoomClaim);
        var room = await _updatingRepository.GetRoom(createRoomClaim.RoomId, _identityProvider.Current.User.UserId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var participantId = createRoomClaim.CharacterId.HasValue
            ? await _characterClaimApprove.GetParticipantId(createRoomClaim.CharacterId.Value, room)
            : await _readerClaimApprove.GetParticipantId(createRoomClaim.ReaderLogin.Trim(), room);
        ;
        var link = _factory.Create(createRoomClaim, participantId);
        var result = await _repository.Create(link);
        await _producer.Send(EventType.ChangedRoom, link.RoomId);

        return result;
    }
}