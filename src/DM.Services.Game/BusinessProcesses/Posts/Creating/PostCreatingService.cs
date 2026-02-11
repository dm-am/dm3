using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using PostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <inheritdoc />
internal class PostCreatingService : IPostCreatingService
{
    private readonly IValidator<CreatePost> _validator;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostFactory _postFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IPostCreatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PostCreatingService(
        IValidator<CreatePost> validator,
        IRoomUpdatingRepository roomUpdatingRepository,
        IIntentionManager intentionManager,
        IPostFactory postFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        IPostCreatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _roomUpdatingRepository = roomUpdatingRepository;
        _intentionManager = intentionManager;
        _postFactory = postFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Post> Create(CreatePost createPost)
    {
        await _validator.ValidateAndThrowAsync(createPost);
        var identity = _identityProvider.Current;
        var room = await _roomUpdatingRepository.GetRoom(createPost.RoomId, identity.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, (room, createPost.CharacterId));

        var events = new List<EventType>(2) {EventType.NewPost};

        var postPendencyUpdates = room.Pendencies
            .Where(p => p.WaitingForUser.UserId == identity.User.UserId)
            .Select(p => _updateBuilderFactory.Create<PostPendency>(p.Id).Delete())
            .ToArray();
        if (postPendencyUpdates.Any())
        {
            events.Add(EventType.RoomPendencyFulfilled);
        }

        var post = _postFactory.Create(createPost, identity.User.UserId);

        var createdPost = await _repository.Create(post, postPendencyUpdates);
        await _unreadCountersRepository.Increment(createdPost.RoomId, UnreadEntryType.Message);
        await _producer.Send(events, createdPost.Id);

        return createdPost;
    }
}