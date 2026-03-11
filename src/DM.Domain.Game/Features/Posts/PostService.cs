using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Rooms;

using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Unified service for post CRUD operations
/// </summary>
internal class PostService : IPostService
{
    private readonly IValidator<CreatePost> _createValidator;
    private readonly IValidator<UpdatePost> _updateValidator;
    private readonly IRoomService _roomService;
    private readonly IRoomRepository _roomRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IPostRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    public PostService(
        IValidator<CreatePost> createValidator,
        IValidator<UpdatePost> updateValidator,
        IRoomService roomService,
        IRoomRepository roomRepository,
        IIntentionManager intentionManager,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IPostRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _roomService = roomService;
        _roomRepository = roomRepository;
        _intentionManager = intentionManager;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    #region Create

    public async Task<Post> CreateAsync(CreatePost createPost)
    {
        await _createValidator.ValidateAndThrowAsync(createPost);
        var identity = _identityProvider.Current;
        var room = await _roomRepository.GetForUpdate(createPost.RoomId, identity.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Room not found");
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, (room, createPost.CharacterId));

        var events = new List<EventType>(2) { EventType.NewPost };

        // Check if this post fulfills any pendencies
        var hasPendencies = room.Pendencies.Any(p => p.WaitingForUser.UserId == identity.User.UserId);
        if (hasPendencies)
            events.Add(EventType.RoomPendencyFulfilled);

        var entity = new CreatePostEntity
        {
            PostId = _guidFactory.Create(),
            RoomId = createPost.RoomId,
            AuthorId = identity.User.UserId,
            CharacterId = createPost.CharacterId,
            Text = createPost.Text.Trim(),
            Comment = createPost.Comment?.Trim(),
            MasterMessage = createPost.MasterMessage?.Trim(),
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdPost = await _repository.Create(entity);
        await _unreadCountersRepository.IncrementAsync(createdPost.RoomId, UnreadEntryType.Message);
        await _producer.SendAsync(events, createdPost.Id);

        return createdPost;
    }

    #endregion

    #region Read

    public async Task<(IEnumerable<Post> posts, PagingResult paging)> GetAllAsync(Guid roomId, PagingQuery query)
    {
        var room = await _roomService.GetAsync(roomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, room);

        var identity = _identityProvider.Current;
        var totalCount = await _repository.Count(roomId, identity.User.UserId);
        var paging = new PagingData(query, identity.Settings.Paging.PostsPerPage, totalCount);
        var posts = await _repository.Get(roomId, paging, identity.User.UserId);

        return (posts, paging.Result);
    }

    public async Task<Post> GetAsync(Guid postId)
    {
        var post = await _repository.Get(postId, _identityProvider.Current.User.UserId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Post not found");
        }
        return post;
    }

    public async Task MarkAsReadAsync(Guid roomId)
    {
        await _roomService.GetAsync(roomId);
        await _unreadCountersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, roomId);
    }

    public Task<BestPostResult?> GetBestPostAsync(Guid userId)
    {
        return _repository.GetBestPost(userId);
    }

    #endregion

    #region Update

    public async Task<Post> UpdateAsync(UpdatePost updatePost)
    {
        await _updateValidator.ValidateAndThrowAsync(updatePost);
        var post = await GetAsync(updatePost.PostId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var room = await _roomRepository.GetForUpdate(post.RoomId, currentUserId);

        var entity = new UpdatePostEntity
        {
            PostId = updatePost.PostId,
            ModifiedUtc = _dateTimeProvider.Now
        };

        // Check text edit permission
        if (_intentionManager.IsAllowed(PostIntention.EditText, (post, room)))
        {
            entity.Text = updatePost.Text.Trim();
            entity.Comment = updatePost.Comment?.Trim();
        }
        else
        {
            entity.Text = post.Text; // Keep original
            entity.Comment = post.Comment;
        }

        // Check master message permission
        if (_intentionManager.IsAllowed(PostIntention.EditMasterMessage, (post, room)))
            entity.MasterMessage = updatePost.MasterMessage?.Trim();
        else
            entity.MasterMessage = post.MasterMessage;

        // Check character change permission
        if (updatePost.CharacterId != null)
        {
            var canChangeCharacter = updatePost.CharacterId.HasChanged(post.Character?.Id) &&
                _intentionManager.IsAllowed(RoomIntention.CreatePost, (room, updatePost.CharacterId.Value)) &&
                _intentionManager.IsAllowed(PostIntention.EditCharacter, (post, room));
            if (canChangeCharacter)
            {
                entity.ShouldChangeCharacter = true;
                entity.CharacterId = updatePost.CharacterId.Value;
            }
        }

        var updatedPost = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedPost, post.Id);

        return updatedPost!;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid postId)
    {
        var post = await GetAsync(postId);
        _intentionManager.ThrowIfForbidden(PostIntention.Delete, post);

        await _repository.Delete(postId);
        await _repository.DecrementAuthorQuantityRating(post.Author.UserId);

        await _unreadCountersRepository.DecrementAsync(post.RoomId, UnreadEntryType.Message, post.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedPost, postId);
    }

    #endregion
}
