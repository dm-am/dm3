using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Content;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Forum.Features.Topics;

/// <inheritdoc />
internal class TopicService : ITopicService
{
    private readonly IValidator<CreateTopic> _createValidator;
    private readonly IValidator<UpdateTopic> _updateValidator;
    private readonly IBoardService _boardService;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ITopicRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IEventProducer _eventProducer;
    private readonly IGuidFactory _guidFactory;
    private readonly ICache _cache;

    public TopicService(
        IValidator<CreateTopic> createValidator,
        IValidator<UpdateTopic> updateValidator,
        IBoardService boardService,
        IAccessPolicyConverter accessPolicyConverter,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ITopicRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IUserLookupService userLookupService,
        IEventProducer eventProducer,
        IGuidFactory guidFactory,
        ICache cache)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _boardService = boardService;
        _accessPolicyConverter = accessPolicyConverter;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _userLookupService = userLookupService;
        _eventProducer = eventProducer;
        _guidFactory = guidFactory;
        _cache = cache;
    }

    // Boards whose listings the home page reads on every cold load. A new topic
    // shows once the short TTL runs out, with no invalidation; keep the set tiny,
    // the cache is per board per query shape.
    private static readonly HashSet<string> CacheableListBoards = new(
        StringComparer.OrdinalIgnoreCase) { "news" };

    /// <inheritdoc />
    public async Task<Topic> CreateAsync(CreateTopic createTopic, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createTopic, ct);

        var board = await _boardService.GetBoard(createTopic.BoardTitle);
        // MayCreate's question, so the button the page offers and this refusal
        // cannot part.
        _intentionManager.ThrowIfForbidden(BoardIntention.CreateTopic, board);

        var author = _identityProvider.Current.User;
        var topicId = _guidFactory.Create();
        var createEntity = new CreateTopicEntity
        {
            TopicId = topicId,
            Title = createTopic.Title,
            // Bodies render on the Comment surface, where [mod] is a mod block.
            Text = ModBlockSanitizer.SanitizeForAuthor(createTopic.Text, author.Role)
        };

        // Markers first, row second, commit on the line after it returns.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.UnderParent(topicId, board.Id, UnreadEntryType.Message));

        var topic = await _repository.Create(
            createEntity,
            author.UserId,
            board.Id,
            ct);
        counters.Commit();

        await _eventProducer.SendAsync(EventType.NewTopic, topic.Id);
        return topic;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The one gate CreateAsync is refused by once the board is found: the
    /// board's policy against the role and the ban are both in it
    /// (BoardIntentionResolver.CreateTopic).
    /// </remarks>
    public bool MayCreate(Board board) =>
        _intentionManager.IsAllowed(BoardIntention.CreateTopic, board);

    /// <inheritdoc />
    /// <remarks>
    /// The policy half of BoardIntentionResolver.CreateTopic, asked for the role
    /// a signed-in member has (a fresh account is not under a ban). A moderator
    /// who is not signed in is not invited on the news board: the page cannot
    /// tell him from anybody else, and the head of the site is where he signs in.
    /// </remarks>
    public bool MayCreateAfterSignIn(Board board) =>
        !_identityProvider.Current.User.IsAuthenticated &&
        (board.CreateTopicPolicy & _accessPolicyConverter.Convert(UserRole.RegularUser)) != BoardAccessPolicy.None;

    /// <inheritdoc />
    public async Task<Topic> GetAsync(Guid topicId, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        var topic = await _repository.Get(topicId, ct);
        if (topic == null)
        {
            throw TopicNotFound(await _repository.Exists(topicId, ct), "Топик не найден");
        }

        await FillUnreadCounter(topic, identity);

        return topic;
    }

    /// <inheritdoc />
    public async Task<Topic> GetByBoardAndNumberAsync(string boardAlias, int topicNumber, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoardByAlias(boardAlias);
        var identity = _identityProvider.Current;

        var topic = await _repository.GetByBoardAndNumber(board.Id, topicNumber, ct);
        if (topic == null)
        {
            throw TopicNotFound(await _repository.ExistsByBoardAndNumber(board.Id, topicNumber, ct),
                $"Топик #{topicNumber} не найден в разделе {boardAlias}");
        }

        await FillUnreadCounter(topic, identity);

        return topic;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAsync(
        string boardTitle, TopicsQuery query, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
        var identity = _identityProvider.Current;

        // The raw board-wide result is cached, and the unread counts are filled per viewer after the read.
        var isCacheableShape =
            CacheableListBoards.Contains(boardTitle) &&
            IsCacheableListingQuery(query);

        PagingData? pagingData = null;
        Topic[] topics;

        if (isCacheableShape)
        {
            // Take keeps the widgets apart.
            var cacheKey = $"topics:list:{board.Id:N}:take={query.Take}";
            // No count: cached listings are small, and the widgets have no paging.
            topics = await _cache.GetOrCreateAsync(
                cacheKey,
                async () => (await _repository.Get(board.Id, pagingData: null, query, ct)).ToArray(),
                CachePolicy.Medium);
        }
        else
        {
            // Attached-only queries have no paging, so no count.
            if (query.IsAttached != true)
            {
                var totalCount = await _repository.Count(board.Id, query, ct);
                pagingData = new PagingData(query, identity.Settings.PageSize, totalCount);
            }

            topics = (await _repository.Get(board.Id, pagingData, query, ct)).ToArray();
        }

        await FillUnreadCounters(topics, identity);

        return (topics, pagingData?.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAcrossBoardsAsync(
        TopicsQuery query, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;

        var totalCount = await _repository.Count(boardId: null, query, ct);
        var pagingData = new PagingData(query, identity.Settings.PageSize, totalCount);

        var topics = (await _repository.Get(boardId: null, pagingData, query, ct)).ToArray();

        await FillUnreadCounters(topics, identity);

        return (topics, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Topic?> GetBestUserTopicAsync(string username, CancellationToken ct = default)
    {
        var user = await _userLookupService.GetAsync(username);

        var identity = _identityProvider.Current;
        var topic = await _repository.GetBestUserTopic(user.UserId, ct);

        if (topic != null)
        {
            await FillUnreadCounter(topic, identity);
        }

        return topic;
    }

    /// <summary>
    /// How many comments of a topic this viewer has not read.
    /// </summary>
    /// <remarks>
    /// Filled per request and never cached. An anonymous viewer has no read state
    /// at all, so the total stands in for it.
    /// </remarks>
    private async Task FillUnreadCounter(Topic topic, IIdentity identity)
    {
        if (identity.User.IsAuthenticated)
        {
            topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntitiesAsync(
                identity.User.UserId, UnreadEntryType.Message, topic.Id))[topic.Id];
        }
        else
        {
            topic.UnreadCommentsCount = topic.TotalCommentsCount;
        }
    }

    /// <inheritdoc cref="FillUnreadCounter" />
    private async Task FillUnreadCounters(ICollection<Topic> topics, IIdentity identity)
    {
        if (identity.User.IsAuthenticated)
        {
            await _unreadCountersRepository.FillEntityCounters(topics, identity.User.UserId,
                t => t.Id, t => t.UnreadCommentsCount);
        }
        else
        {
            foreach (var topic in topics)
            {
                topic.UnreadCommentsCount = topic.TotalCommentsCount;
            }
        }
    }

    /// <summary>
    /// A listing query is cache-friendly when it has no dynamic filters and uses
    /// the default page size or smaller: filters would blow up the cache key space.
    /// </summary>
    private static bool IsCacheableListingQuery(TopicsQuery query) =>
        query.IsAttached != true &&
        string.IsNullOrEmpty(query.Search) &&
        (query.AuthorUsernames == null || query.AuthorUsernames.Count == 0) &&
        !query.CreatedFromUtc.HasValue &&
        !query.CreatedToUtc.HasValue &&
        string.IsNullOrEmpty(query.SortBy) &&
        string.IsNullOrEmpty(query.SortOrder) &&
        query.Skip == 0 &&
        query.Take is > 0 and <= 20;

    /// <inheritdoc />
    // No access check: the caller passes ids of topics it has already read
    // through an access-filtered path, and a marker carries nothing beyond the
    // period the topic summarizes.
    public Task<IReadOnlyDictionary<Guid, PeriodDigest>> GetPeriodDigestsAsync(
        IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default) =>
        _repository.GetPeriodDigests(topicIds, ct);

    /// <inheritdoc />
    public async Task<Topic> UpdateAsync(UpdateTopic updateTopic, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(updateTopic, ct);
        var oldTopic = await GetAsync(updateTopic.TopicId, ct);

        // A request that reopens the topic is not refused for the topic being
        // closed: whoever may reopen a topic may edit it once it is open.
        var changesClosed = updateTopic.IsClosed.HasValue && updateTopic.IsClosed != oldTopic.IsClosed;
        if (changesClosed)
        {
            _intentionManager.ThrowIfForbidden(TopicIntention.Close, oldTopic);
        }

        if (!changesClosed || updateTopic.IsClosed == true)
        {
            _intentionManager.ThrowIfForbidden(TopicIntention.Edit, oldTopic);
        }

        var newBoardId = await ResolveAdministration(oldTopic, updateTopic);
        var currentUser = _identityProvider.Current.User;

        var bodyText = updateTopic.Text;
        // Bodies render on the Comment surface, where [mod] is a mod block.
        if (!string.IsNullOrEmpty(bodyText))
        {
            bodyText = ModBlockSanitizer.SanitizeForAuthor(bodyText, currentUser.Role);
        }

        var updateEntity = new UpdateTopicEntity
        {
            TopicId = updateTopic.TopicId,
            Title = updateTopic.Title,
            Text = bodyText,
            IsClosed = updateTopic.IsClosed,
            // Who closed the topic decides whether its author may reopen it
            // (TopicIntention.Close), so it is written with every change of the
            // closed state and left alone otherwise.
            ClosedByAuthor = changesClosed
                ? updateTopic.IsClosed == true && oldTopic.Author?.UserId == currentUser.UserId
                : null,
            IsAttached = updateTopic.IsAttached,
            // Not necessarily the author, and the actor the ChangedTopic notification is filtered by.
            EditorUserId = currentUser.UserId
        };
        var updated = await _repository.Update(updateEntity, newBoardId);

        // Announced only when the row moved: the actor is read from the edit history,
        // which a request handing back the same values leaves untouched.
        if (updated.Changed)
        {
            await _eventProducer.SendAsync(EventType.ChangedTopic, updated.Topic.Id);
        }

        return updated.Topic;
    }

    /// <summary>
    /// The part of an update only the board's administrators may ask for: moving
    /// the topic to another board and pinning it.
    /// </summary>
    /// <remarks>
    /// Asked by anybody else, either one is refused, and the pin the request
    /// round-trips is taken out of it.
    /// </remarks>
    /// <returns>The board the topic moves to, or null when it stays.</returns>
    private async Task<Guid?> ResolveAdministration(Topic oldTopic, UpdateTopic updateTopic)
    {
        Guid? newBoardId = null;

        if (_intentionManager.IsAllowed(BoardIntention.AdministrateTopics, oldTopic.Board))
        {
            if (updateTopic.BoardTitle != null &&
                oldTopic.Board.Title != updateTopic.BoardTitle)
            {
                var board = await _boardService.GetBoard(updateTopic.BoardTitle);
                _intentionManager.ThrowIfForbidden(BoardIntention.CreateTopic, board);
                newBoardId = board.Id;
                await _unreadCountersRepository.ChangeParentAsync(oldTopic.Id, UnreadEntryType.Message, board.Id);
            }
        }
        else
        {
            // Asking for one of these is refused; round-tripping the value the topic already has is not.
            if ((updateTopic.IsAttached.HasValue && updateTopic.IsAttached != oldTopic.IsAttached) ||
                (updateTopic.BoardTitle != null && oldTopic.Board.Title != updateTopic.BoardTitle))
            {
                _intentionManager.ThrowIfForbidden(BoardIntention.AdministrateTopics, oldTopic.Board);
            }

            updateTopic.IsAttached = null;
        }

        return newBoardId;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await GetAsync(topicId, ct);
        _intentionManager.ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board);

        await _repository.Delete(topicId, _identityProvider.Current.User.UserId);
        await _unreadCountersRepository.DeleteAsync(topicId, UnreadEntryType.Message);
        await _eventProducer.SendAsync(EventType.DeletedTopic, topicId);
    }

    /// <inheritdoc />
    public async Task ReorderPinnedAsync(string boardTitle, IReadOnlyList<Guid> topicIds, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
        _intentionManager.ThrowIfForbidden(BoardIntention.AdministrateTopics, board);

        // The order of the board as a whole: a subset would leave the topics it
        // skipped on the positions this request has just handed to others.
        var pinned = await _repository.GetAttachedTopicIds(board.Id, ct);
        var named = new HashSet<Guid>(topicIds);
        if (named.Count != topicIds.Count || !named.SetEquals(pinned))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["topicIds"] = "Порядок должен перечислять все закрепленные топики раздела, каждый по одному разу"
            });
        }

        await _repository.ReplaceAttachOrder(board.Id, topicIds, ct);
    }

    /// <summary>
    /// 410 only for a topic that existed and was removed; one that never existed is an honest 404.
    /// </summary>
    private static HttpException TopicNotFound(bool existed, string refusal) =>
        new(existed ? HttpStatusCode.Gone : HttpStatusCode.NotFound, refusal);
}
