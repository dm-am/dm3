#pragma warning disable CS8603 // MaybeField chaining produces false positive null warnings
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Posts.Reading;
using DM.Services.Game.BusinessProcesses.Rooms.Updating;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;

namespace DM.Services.Game.BusinessProcesses.Posts.Updating;

/// <inheritdoc />
internal class PostUpdatingService : IPostUpdatingService
{
    private readonly IValidator<UpdatePost> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPostReadingService _postReadingService;
    private readonly IRoomUpdatingRepository _roomUpdatingRepository;
    private readonly IPostUpdatingRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PostUpdatingService(
        IValidator<UpdatePost> validator,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IDateTimeProvider dateTimeProvider,
        IPostReadingService postReadingService,
        IRoomUpdatingRepository roomUpdatingRepository,
        IPostUpdatingRepository repository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _dateTimeProvider = dateTimeProvider;
        _postReadingService = postReadingService;
        _roomUpdatingRepository = roomUpdatingRepository;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Post> Update(UpdatePost updatePost)
    {
        await _validator.ValidateAndThrowAsync(updatePost);
        var post = await _postReadingService.Get(updatePost.PostId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _roomUpdatingRepository.GetRoom(post.RoomId, currentUserId);

        var updateBuilder = _updateBuilderFactory.Create<DbPost>(updatePost.PostId);

        if (_intentionManager.IsAllowed(PostIntention.EditText, (post, room)))
        {
            updateBuilder
                .MaybeField(p => p.Text, updatePost.Text)
                .MaybeField(p => p.Commentary, updatePost.Commentary);

            if (_intentionManager.IsAllowed(PostIntention.EditMasterMessage, (post, room)))
            {
                updateBuilder.MaybeField(p => p.MasterMessage, updatePost.MasterMessage);
            }
        }

        if (updatePost.CharacterId != null &&
            updatePost.CharacterId.HasChanged(post.Character?.Id) &&
            _intentionManager.IsAllowed(RoomIntention.CreatePost, (room, updatePost.CharacterId.Value)) &&
            _intentionManager.IsAllowed(PostIntention.EditCharacter, (post, room)))
        {
            updateBuilder.MaybeField(p => p.CharacterId, updatePost.CharacterId);
        }

        if (updateBuilder.HasChanges())
        {
            updateBuilder
                .Field(p => p.ModifiedUtc, _dateTimeProvider.Now)
                .Field(p => p.ModifiedByUserId, currentUserId);
        }

        var updatedPost = await _repository.Update(updateBuilder);
        await _producer.Send(EventType.ChangedPost, post.Id);

        return updatedPost!;
    }
}