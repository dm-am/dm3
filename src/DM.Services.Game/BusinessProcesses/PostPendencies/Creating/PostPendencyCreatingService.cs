using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using DM.Services.Game.BusinessProcesses.Rooms.Reading;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <inheritdoc />
internal class PostPendencyCreatingService : IPostPendencyCreatingService
{
    private readonly IValidator<CreatePostPendency> _validator;
    private readonly IRoomReadingService _roomReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostPendencyFactory _factory;
    private readonly IUserRepository _userRepository;
    private readonly IPostPendencyCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PostPendencyCreatingService(
        IValidator<CreatePostPendency> validator,
        IRoomReadingService roomReadingService,
        IIntentionManager intentionManager,
        IPostPendencyFactory factory,
        IUserRepository userRepository,
        IPostPendencyCreatingRepository repository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _roomReadingService = roomReadingService;
        _intentionManager = intentionManager;
        _factory = factory;
        _userRepository = userRepository;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<PostPendency> Create(CreatePostPendency createPostPendency)
    {
        await _validator.ValidateAndThrowAsync(createPostPendency);
        var room = await _roomReadingService.Get(createPostPendency.RoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePostPendency, room);

        var (_, waitingForUserId) = await _userRepository.FindUserId(createPostPendency.WaitingForUserLogin);
        var currentUserId = _identityProvider.Current.User.UserId;
        if (room.Pendencies.Any(e =>
                e.CreatedBy.UserId == currentUserId &&
                e.WaitingForUser.UserId == waitingForUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"There's already a pendency for user {createPostPendency.WaitingForUserLogin}");
        }

        if (room.Accesses.All(a => a.Character.Author.UserId != waitingForUserId))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [nameof(PostPendency.WaitingForUser)] = ValidationError.Invalid
            });
        }

        var pendencyToCreate = _factory.Create(createPostPendency, currentUserId, waitingForUserId);

        var pendency = await _repository.Create(pendencyToCreate);
        await _producer.Send(EventType.RoomPendencyCreated, pendency.Id);

        return pendency;
    }
}
