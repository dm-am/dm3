using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
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
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IDiceRoller _diceRoller;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IPrivateBlockReading _privateBlockReading;

    public PostService(
        IValidator<CreatePost> createValidator,
        IValidator<UpdatePost> updateValidator,
        IRoomService roomService,
        IRoomRepository roomRepository,
        IIntentionManager intentionManager,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IPostRepository repository,
        IDiceRollRepository diceRollRepository,
        IDiceRoller diceRoller,
        IUnreadCountersRepository unreadCountersRepository,
        IEventProducer producer,
        IIdentityProvider identityProvider,
        IPrivateBlockReading privateBlockReading)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _roomService = roomService;
        _roomRepository = roomRepository;
        _intentionManager = intentionManager;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _repository = repository;
        _diceRollRepository = diceRollRepository;
        _diceRoller = diceRoller;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
        _privateBlockReading = privateBlockReading;
    }

    #region Create

    public async Task<Post> CreateAsync(CreatePost createPost)
    {
        await _createValidator.ValidateAndThrowAsync(createPost);
        var identity = _identityProvider.Current;
        var room = await _roomRepository.GetForUpdate(createPost.RoomId, identity.User.UserId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, (room, createPost.CharacterId));

        var events = new List<EventType>(2) { EventType.NewPost };

        var hasPendencies = room.Pendencies.Any(p => p.WaitingForUser.UserId == identity.User.UserId);
        if (hasPendencies)
        {
            events.Add(EventType.RoomPendencyFulfilled);
        }

        // Only the metagame text is sanitized: the game text renders on the GamePost surface, which reads no [mod].
        var metaText = AuthoredText.AsTyped(createPost.MetagameText);
        if (!string.IsNullOrEmpty(metaText))
        {
            metaText = ModBlockSanitizer.SanitizeForAuthor(metaText, identity.User.Role);
        }

        var now = _dateTimeProvider.Now;
        var gameText = createPost.GameText;
        var postId = _guidFactory.Create();

        // Dice are dropped when the room has rolling disabled, whatever the payload says.
        // A roll cannot be produced again, so it is written in the post's transaction.
        var diceSpecs = createPost.DiceRolls.ToList();
        var rolls = diceSpecs.Count > 0 && room.Settings.DiceEnabled
            ? _diceRoller.Roll(postId, now, diceSpecs)
            : [];

        var entity = new CreatePostEntity
        {
            PostId = postId,
            RoomId = createPost.RoomId,
            AuthorId = identity.User.UserId,
            CharacterId = createPost.CharacterId,
            GameText = gameText,
            MetagameText = metaText,
            PrivateAddresseeSnapshotJson = ResolvePrivateAddressees(gameText, room, null),
            CreatedUtc = now,
            DiceRolls = rolls
        };

        var createdPost = await _repository.Create(entity);

        if (rolls.Count > 0)
        {
            createdPost.DiceRolls = rolls;
        }

        await _unreadCountersRepository.IncrementAsync(createdPost.RoomId, UnreadEntryType.Message);
        await _producer.SendAsync(events, createdPost.Id);

        return createdPost;
    }

    #endregion

    #region Read

    public async Task<(IEnumerable<Post> posts, PagingResult paging)> GetAllAsync(Guid roomId, RoomPostsQuery query)
    {
        // Reading is gated by the scope the room is fetched through: a room outside it is a 404.
        await _roomService.GetAsync(roomId);

        var identity = _identityProvider.Current;
        var totalCount = await _repository.Count(roomId, query, identity.User.UserId);
        var paging = new PagingData(query, identity.Settings.PageSize, totalCount);
        var posts = (await _repository.Get(roomId, query, paging, identity.User.UserId)).ToList();

        await EnrichWithDiceRollsAsync(posts, identity.User.UserId);

        return (posts, paging.Result);
    }

    public async Task<Post> GetAsync(Guid postId, CancellationToken ct = default)
    {
        var readerId = _identityProvider.Current.User.UserId;
        var post = await _repository.Get(postId, readerId, ct);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        post.DiceRolls = (await _diceRollRepository.GetByPostIdAsync(postId, ct)).VisibleTo(post, readerId);

        return post;
    }

    public async Task MarkAsReadAsync(Guid roomId)
    {
        await _roomService.GetAsync(roomId);
        await _unreadCountersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, roomId);
    }

    public async Task<(IEnumerable<Post> Posts, PagingResult Paging)> GetRatedAsync(PostsQuery query)
    {
        var (posts, totalCount) = await _repository.GetRated(query,
            _identityProvider.Current.User.UserId);
        var paging = new PagingData(query, query.Take, totalCount);
        return (posts, paging.Result);
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
            PostEditId = _guidFactory.Create(),
            EditorUserId = currentUserId,
            EditedUtc = _dateTimeProvider.Now
        };

        // Submitted text the caller may not edit is refused; no text keeps the stored values.
        if (updatePost.GameText != null || updatePost.MetagameText != null)
        {
            _intentionManager.ThrowIfForbidden(PostIntention.EditText, (post, room));

            // An absent field keeps its value, an empty string clears it.
            entity.GameText = updatePost.GameText == null
                ? post.GameText
                : updatePost.GameText;
            var metaText = AuthoredText.AsTyped(updatePost.MetagameText);
            if (!string.IsNullOrEmpty(metaText))
            {
                metaText = ModBlockSanitizer.SanitizeForAuthor(metaText, _identityProvider.Current.User.Role);
            }

            entity.MetagameText = updatePost.MetagameText == null ? post.MetagameText : metaText;

            if (updatePost.GameText != null)
            {
                ThrowIfFileForAllOnlyInsideBlocks(entity.GameText, post);
            }
        }
        else
        {
            entity.GameText = post.GameText;
            entity.MetagameText = post.MetagameText;
        }

        entity.PrivateAddresseeSnapshotJson = ResolvePrivateAddressees(
            entity.GameText, room, post.PrivateAddresseeSnapshotJson);

        // A change the caller may not make is a refusal, an unchanged value is not a change.
        if (updatePost.CharacterId != null && updatePost.CharacterId.HasChanged(post.Character?.Id))
        {
            _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, (room, updatePost.CharacterId.Value));
            _intentionManager.ThrowIfForbidden(PostIntention.EditCharacter, (post, room));

            entity.ShouldChangeCharacter = true;
            entity.CharacterId = updatePost.CharacterId.Value;
        }

        var updatedPost = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedPost, post.Id);

        return updatedPost!;
    }

    /// <remarks>
    /// Names resolve against the characters with access to the room, so a snapshot hands out no access.
    /// Blocks an earlier save resolved keep their ids: the rule is addressee-forever. The character travels
    /// beside its name, so a name taken by somebody else is refused rather than delivered.
    /// </remarks>
    private static string ResolvePrivateAddressees(
        string gameText, RoomToUpdate? room, string? previousSnapshotJson) =>
        PrivateAddresseeSnapshot.Build(gameText, previousSnapshotJson, room?.PrivateAddressees() ?? []);

    public IReadOnlySet<Guid> Editable(IEnumerable<Post> posts) =>
        posts.Where(p => _intentionManager.IsAllowed(PostIntention.EditText, p)).Select(p => p.Id).ToHashSet();

    /// <summary>
    /// Refuse a text that puts a file every reader sees only inside hidden blocks.
    /// </summary>
    /// <remarks>
    /// The plate of the file would say "Все" while the text gives it to the readers of the blocks, and the
    /// read of the file keeps it to them. The author is asked which of the two he means, rather than told
    /// one thing and shown another; what he stored stays as he stored it.
    /// </remarks>
    private void ThrowIfFileForAllOnlyInsideBlocks(string gameText, Post post)
    {
        var forAll = post.Attachments.Where(a => a.AddresseesJson == null).ToArray();
        if (forAll.Length == 0)
        {
            return;
        }

        var inside = _privateBlockReading.OnlyInsideBlocks(gameText, forAll.Select(a => a.Id).ToArray());
        if (forAll.FirstOrDefault(a => inside.Contains(a.Id)) is { } attachment)
        {
            throw new HttpException(
                HttpStatusCode.BadRequest, RefusalMessage.FileForAllOnlyInsideHiddenBlock(attachment.FileName));
        }
    }

    private async Task EnrichWithDiceRollsAsync(List<Post> posts, Guid readerId)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postIds = posts.Select(p => p.Id).ToList();
        var diceRollsByPost = await _diceRollRepository.GetByPostIdsAsync(postIds);

        foreach (var post in posts)
        {
            if (diceRollsByPost.TryGetValue(post.Id, out var rolls))
            {
                post.DiceRolls = rolls.VisibleTo(post, readerId);
            }
        }
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid postId)
    {
        var post = await GetAsync(postId);
        _intentionManager.ThrowIfForbidden(PostIntention.Delete, post);

        var removed = await _repository.Delete(postId, _identityProvider.Current.User.UserId);
        if (!removed)
        {
            // Removed by somebody else in between: the counter and the event belong to that removal.
            return;
        }

        await _unreadCountersRepository.DecrementAsync(post.RoomId, UnreadEntryType.Message, post.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedPost, postId);
    }

    #endregion
}
