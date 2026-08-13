using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IDiceRoller _diceRoller;
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
        IDiceRollRepository diceRollRepository,
        IDiceRoller diceRoller,
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
        _diceRollRepository = diceRollRepository;
        _diceRoller = diceRoller;
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
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, (room, createPost.CharacterId));

        var events = new List<EventType>(2) { EventType.NewPost };

        // Check if this post fulfills any pendencies
        var hasPendencies = room.Pendencies.Any(p => p.WaitingForUser.UserId == identity.User.UserId);
        if (hasPendencies)
            events.Add(EventType.RoomPendencyFulfilled);

        var metaText = createPost.MetagameText?.Trim();
        // MetagameText (OOC) renders on the Comment surface where [mod] is a
        // green mod block; strip it when authored by a non-moderator. GameText
        // renders on the GamePost surface (no [mod]) and is left as-is.
        if (!string.IsNullOrEmpty(metaText))
            metaText = ModBlockSanitizer.SanitizeForAuthor(metaText, identity.User.Role);

        var now = _dateTimeProvider.Now;
        var gameText = createPost.GameText.Trim();

        var entity = new CreatePostEntity
        {
            PostId = _guidFactory.Create(),
            RoomId = createPost.RoomId,
            AuthorId = identity.User.UserId,
            CharacterId = createPost.CharacterId,
            GameText = gameText,
            MetagameText = metaText,
            PrivateAddresseeSnapshotJson = ResolvePrivateAddressees(gameText, room, null),
            CreatedUtc = now
        };

        // Roll and persist any requested dice server-side (doc 4.2.2.13).
        // The client already gates the composer on the room setting, but the
        // server must not trust the payload — dice are dropped when the room
        // has rolling disabled.
        //
        // Rolled and stored before the post, and undone if the post does not
        // follow. There is no transaction across PostgreSQL and MongoDB and no
        // outbox — DATA_STORAGE.md says both — so a feature living in two stores
        // owes an explicit order: what is written first, and who clears the
        // remainder. A roll is the one thing here that cannot be produced again,
        // because rolling a second time answers a different number: written after
        // the post, a failed Mongo call left a committed post whose dice are gone
        // for good, and the author has no way to get the same throw back. Written
        // first, the same failure loses a post nobody has seen and the author may
        // simply post again. The post id is ours already, generated above.
        var diceSpecs = createPost.DiceRolls?.ToList() ?? new List<CreatePostDiceRoll>();
        var rolls = diceSpecs.Count > 0 && room.Settings?.DiceEnabled == true
            ? _diceRoller.Roll(entity.PostId, now, diceSpecs)
            : [];

        if (rolls.Count > 0)
        {
            await _diceRollRepository.CreateAsync(rolls);
        }

        Post createdPost;
        try
        {
            createdPost = await _repository.Create(entity);
        }
        catch
        {
            if (rolls.Count > 0)
            {
                await _diceRollRepository.DeleteByPostIdAsync(entity.PostId);
            }
            throw;
        }

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

    public async Task<(IEnumerable<Post> posts, PagingResult paging)> GetAllAsync(Guid roomId, PagingQuery query)
    {
        // Reading is gated by the scope the room was fetched through:
        // GameAccessibilityFilters.RoomAvailable admits an open room to anyone
        // who may see the game, and a private one only to its leads and to the
        // characters and readers the room was opened to. A room outside that
        // scope is not refused, it is absent, and GetAsync answers 404 for it.
        //
        // What stood here asked whether the reader may CREATE a post, against a
        // target type no resolver handles, so every read of every room answered
        // 403 and the room page said it could not load its posts.
        var room = await _roomService.GetAsync(roomId);

        var identity = _identityProvider.Current;
        var totalCount = await _repository.Count(roomId, identity.User.UserId);
        var paging = new PagingData(query, identity.Settings.Paging.PostsPerPage, totalCount);
        var posts = (await _repository.Get(roomId, paging, identity.User.UserId)).ToList();

        // Enrich posts with dice rolls
        await EnrichWithDiceRollsAsync(posts);

        return (posts, paging.Result);
    }

    public async Task<Post> GetAsync(Guid postId)
    {
        var post = await _repository.Get(postId, _identityProvider.Current.User.UserId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        // Enrich post with dice rolls
        post.DiceRolls = await _diceRollRepository.GetByPostIdAsync(postId);

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
            PostId = updatePost.PostId
        };

        // Submitted text the caller may not edit is refused. It used to be swapped
        // for the stored text and saved, so an edit nobody was allowed to make came
        // back 200 with the post as it was - the same response a successful edit
        // produces. A request that submits no text asks for nothing and keeps the
        // stored values: the lead who may only change the character reaches the
        // branch below.
        if (updatePost.GameText != null || updatePost.MetagameText != null)
        {
            _intentionManager.ThrowIfForbidden(PostIntention.EditText, (post, room));

            // PATCH semantics: an absent field keeps its value, an empty string
            // clears it. Without the null check, editing only the in-game text
            // wiped the out-of-character text, and omitting the in-game text threw.
            entity.GameText = updatePost.GameText?.Trim() ?? post.GameText;
            var metaText = updatePost.MetagameText?.Trim();
            // MetagameText (OOC) renders on the Comment surface where [mod] is
            // a green mod block; strip it when the editor is a non-moderator.
            if (!string.IsNullOrEmpty(metaText))
                metaText = ModBlockSanitizer.SanitizeForAuthor(
                    metaText, _identityProvider.Current.User.Role);
            entity.MetagameText = updatePost.MetagameText == null ? post.MetagameText : metaText;
        }
        else
        {
            entity.GameText = post.GameText; // Keep original
            entity.MetagameText = post.MetagameText;
        }

        entity.PrivateAddresseeSnapshotJson = ResolvePrivateAddressees(
            entity.GameText, room, post.PrivateAddresseeSnapshotJson);

        // Same rule for the character: a change the caller may not make is a
        // refusal, an unchanged value is not a change.
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

    /// <summary>
    /// Freeze who each [private=...] block of the post is for. Without this the
    /// snapshot stayed at its default and the addressee rule — one of the five
    /// in BBCODE_RENDERING.md — never fired for anyone: the player a line was
    /// written to was the one reader who could not read it.
    /// </summary>
    /// <remarks>
    /// Names resolve against the characters that have access to the room, so a
    /// block can only ever name someone who already reads it; a snapshot hands
    /// out no access that room membership did not. Blocks an earlier save
    /// resolved keep their ids — the rule is addressee-forever, and re-resolving
    /// them on edit would revoke a player whose character has left since.
    /// </remarks>
    private static string ResolvePrivateAddressees(
        string gameText, RoomToUpdate? room, string? previousSnapshotJson) =>
        PrivateAddresseeSnapshot.Build(gameText, previousSnapshotJson,
            room?.Accesses
                .Where(a => a.Character is not null && a.Character.Author is not null)
                .Select(a => new PrivateAddressee(a.Character.Name, a.Character.Author.UserId))
            ?? []);

    private async Task EnrichWithDiceRollsAsync(List<Post> posts)
    {
        if (posts.Count == 0) return;

        var postIds = posts.Select(p => p.Id).ToList();
        var diceRollsByPost = await _diceRollRepository.GetByPostIdsAsync(postIds);

        foreach (var post in posts)
        {
            if (diceRollsByPost.TryGetValue(post.Id, out var rolls))
                post.DiceRolls = rolls;
        }
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid postId)
    {
        var post = await GetAsync(postId);
        _intentionManager.ThrowIfForbidden(PostIntention.Delete, post);

        // The author's post count moves inside Delete, in the transaction that
        // removes the post: asked for separately here, the two could and did
        // commit apart.
        await _repository.Delete(postId, _identityProvider.Current.User.UserId);

        await _unreadCountersRepository.DecrementAsync(post.RoomId, UnreadEntryType.Message, post.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedPost, postId);
    }

    #endregion
}
