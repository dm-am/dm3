using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <inheritdoc />
internal class RoomAccessCreatingService : IRoomAccessCreatingService
{
    private readonly IValidator<CreateRoomAccess> _validator;
    private readonly IRoomUpdatingRepository _updatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IRoomAccessFactory _factory;
    private readonly ICharacterClaimApprove _characterClaimApprove;
    private readonly IReaderClaimApprove _readerClaimApprove;
    private readonly IRoomAccessCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public RoomAccessCreatingService(
        IValidator<CreateRoomAccess> validator,
        IRoomUpdatingRepository updatingRepository,
        IIntentionManager intentionManager,
        IRoomAccessFactory factory,
        ICharacterClaimApprove characterClaimApprove,
        IReaderClaimApprove readerClaimApprove,
        IRoomAccessCreatingRepository repository,
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
    public async Task<RoomAccess> Create(CreateRoomAccess createRoomAccess)
    {
        await _validator.ValidateAndThrowAsync(createRoomAccess);
        var room = await _updatingRepository.GetRoom(createRoomAccess.RoomId, _identityProvider.Current.User.UserId);
        if (room == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Edit, room.Game);

        var participantId = createRoomAccess.CharacterId.HasValue
            ? await _characterClaimApprove.GetParticipantId(createRoomAccess.CharacterId.Value, room)
            : await _readerClaimApprove.GetParticipantId(createRoomAccess.ReaderLogin.Trim(), room);
        ;
        var link = _factory.Create(createRoomAccess, participantId);
        var result = await _repository.Create(link);
        await _producer.Send(EventType.ChangedRoom, link.RoomId);

        return result;
    }
}