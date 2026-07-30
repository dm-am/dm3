using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IEventProducer _invokedEventProducer;
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
        IEventProducer invokedEventProducer,
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
        _invokedEventProducer = invokedEventProducer;
        _cache = cache;
    }

    // Aliases of boards whose listings are cheap to cache at the service
    // layer: they are hit from the home page on every cold load, the
    // content changes at most a few times a day, and a 30–60 second
    // staleness window is acceptable. Keep this set tiny — the cache is
    // per-board-alias per-query-shape, so adding boards blindly inflates
    // the cache surface.
    private static readonly HashSet<string> CacheableListBoards = new(
        StringComparer.OrdinalIgnoreCase) { "news" };

    /// <inheritdoc />
    public async Task<Topic> CreateAsync(CreateTopic createTopic, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createTopic, ct);

        var board = await _boardService.GetBoard(createTopic.BoardTitle);
        _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);

        var author = _identityProvider.Current.User;
        var createEntity = new CreateTopicEntity
        {
            Title = createTopic.Title,
            // Topic bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = ModBlockSanitizer.SanitizeForAuthor(createTopic.Text, author.Role)
        };
        var topic = await _repository.Create(
            createEntity,
            author.UserId,
            board.Id,
            ct);

        await Task.WhenAll(
            _invokedEventProducer.SendAsync(EventType.NewTopic, topic.Id),
            _unreadCountersRepository.CreateAsync(topic.Id, board.Id, UnreadEntryType.Message));

        // The cacheable-boards listing fast path uses a short TTL
        // (CachePolicy.Medium = 1 min) so a fresh topic becomes visible
        // within 60 seconds with no explicit invalidation. This is
        // acceptable for the news board — topics are rare, staleness
        // bounded, and explicit invalidation would require iterating
        // every (take, accessPolicy) cache key combination.
        return topic;
    }

    /// <inheritdoc />
    public async Task<Topic> GetAsync(Guid topicId, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
        var topic = await _repository.Get(topicId, accessPolicy, ct);
        if (topic == null)
        {
            // 410 only for a topic that genuinely existed and was removed;
            // an id that never existed is an honest 404.
            throw new HttpException(
                await _repository.Exists(topicId, ct)
                    ? HttpStatusCode.Gone
                    : HttpStatusCode.NotFound,
                "Тема не найдена");
        }

        if (identity.User.IsAuthenticated)
        {
            topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntitiesAsync(
                identity.User.UserId, UnreadEntryType.Message, topicId))[topicId];
        }
        else
        {
            // Anonymous users: show total counts
            topic.UnreadCommentsCount = topic.TotalCommentsCount;
        }

        return topic;
    }

    /// <inheritdoc />
    public async Task<Topic> GetByBoardAndNumberAsync(string boardAlias, int topicNumber, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoardByAlias(boardAlias);
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);

        var topic = await _repository.GetByBoardAndNumber(board.Id, topicNumber, accessPolicy, ct);
        if (topic == null)
        {
            // 410 only for a topic that genuinely existed and was removed;
            // a number that never existed is an honest 404.
            throw new HttpException(
                await _repository.ExistsByBoardAndNumber(board.Id, topicNumber, ct)
                    ? HttpStatusCode.Gone
                    : HttpStatusCode.NotFound,
                $"Тема #{topicNumber} не найдена в разделе {boardAlias}");
        }

        if (identity.User.IsAuthenticated)
        {
            topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntitiesAsync(
                identity.User.UserId, UnreadEntryType.Message, topic.Id))[topic.Id];
        }
        else
        {
            topic.UnreadCommentsCount = topic.TotalCommentsCount;
        }

        return topic;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAsync(
        string boardTitle, TopicsQuery query, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
        var identity = _identityProvider.Current;

        // Fast path for cacheable boards (home page widgets). When the
        // query has no filters / search / author / date filters and uses
        // default paging, we can cache the raw board-wide result for a
        // short window and still give the caller correct per-user unread
        // counts by filling them after the cache read.
        var isCacheableShape =
            CacheableListBoards.Contains(boardTitle) &&
            IsCacheableListingQuery(query);

        PagingData? pagingData = null;
        Topic[] topics;

        if (isCacheableShape)
        {
            // Cache key includes board alias + take to keep per-widget
            // variants (e.g. take=5 for the home page, take=20 for the
            // news board page) on separate entries, and access policy so
            // guests and mentors never share a cache entry with more
            // privileged viewers.
            var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
            var cacheKey = $"topics:list:{board.Id:N}:take={query.Take}:ap={(int)accessPolicy}";
            topics = await _cache.GetOrCreateAsync(
                cacheKey,
                async () => await LoadListingAsync(board.Id, accessPolicy, query, ct),
                CachePolicy.Medium);
        }
        else
        {
            // Regular path: count + fetch with full paging data. For
            // attached-only queries we skip the count (there is no paging UI).
            var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
            if (query.IsAttached != true)
            {
                var totalCount = await _repository.Count(board.Id, accessPolicy, query, ct);
                pagingData = new PagingData(query, identity.Settings.Paging.TopicsPerPage, totalCount);
            }

            topics = (await _repository.Get(board.Id, accessPolicy, pagingData, query, ct)).ToArray();
        }

        if (identity.User.IsAuthenticated)
        {
            // Unread counts are always filled per-request, never cached:
            // they depend on the specific viewer's read state.
            await _unreadCountersRepository.FillEntityCounters(topics, identity.User.UserId,
                t => t.Id, t => t.UnreadCommentsCount);
        }
        else
        {
            // Anonymous users: show total counts
            foreach (var topic in topics)
            {
                topic.UnreadCommentsCount = topic.TotalCommentsCount;
            }
        }

        return (topics, pagingData?.Result);
    }

    /// <summary>
    /// Deterministic loader for the cacheable-listing fast path. Skips
    /// the <c>SELECT COUNT(*)</c> round-trip entirely — cached listings
    /// are small (take &lt;= 20), and the home-page widgets do not need
    /// pagination metadata. Access policy is plumbed through the cache key
    /// at the caller, so this helper just forwards it to the repository.
    /// </summary>
    private async Task<Topic[]> LoadListingAsync(Guid boardId, BoardAccessPolicy accessPolicy, TopicsQuery query, CancellationToken ct) =>
        (await _repository.Get(boardId, accessPolicy, pagingData: null, query, ct)).ToArray();

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult? paging)> GetListAcrossBoardsAsync(
        TopicsQuery query, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);

        // Count over the access-policy-scoped result set so the paging
        // total matches the rows the user actually sees.
        var totalCount = await _repository.Count(boardId: null, accessPolicy, query, ct);
        var pagingData = new PagingData(query, identity.Settings.Paging.TopicsPerPage, totalCount);

        var topics = (await _repository.Get(boardId: null, accessPolicy, pagingData, query, ct)).ToArray();

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

        return (topics, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Topic?> GetBestUserTopicAsync(string username, CancellationToken ct = default)
    {
        // Resolve username → UserId via the cross-module lookup so the
        // repository stays typed on Guid. Throws HttpException(410) on an
        // unknown user, which surfaces as a clean 404 to the API caller.
        var user = await _userLookupService.GetAsync(username);

        // Scope to boards the current viewer can see — the same access-policy
        // mask the cross-board listing uses, so the widget never surfaces a
        // topic on a board the viewer lacks access to.
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
        var topic = await _repository.GetBestUserTopic(user.UserId, accessPolicy, ct);

        if (topic != null)
        {
            if (identity.User.IsAuthenticated)
            {
                topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntitiesAsync(
                    identity.User.UserId, UnreadEntryType.Message, topic.Id))[topic.Id];
            }
            else
            {
                // Anonymous users: show total counts
                topic.UnreadCommentsCount = topic.TotalCommentsCount;
            }
        }

        return topic;
    }

    /// <summary>
    /// A listing query is cache-friendly when it has no dynamic filters
    /// and uses the default page size (or smaller). Search, author filters,
    /// and date ranges would blow up the cache key space; attached-only
    /// queries skip the normal paging path anyway.
    /// </summary>
    private static bool IsCacheableListingQuery(TopicsQuery query) =>
        query.IsAttached != true &&
        string.IsNullOrEmpty(query.Search) &&
        (query.Authors == null || query.Authors.Count == 0) &&
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

        _intentionManager.ThrowIfForbidden(TopicIntention.Edit, oldTopic);

        Guid? newBoardId = null;

        if (_intentionManager.IsAllowed(ForumIntention.AdministrateTopics, oldTopic.Board))
        {
            if (updateTopic.BoardTitle != default &&
                oldTopic.Board.Title != updateTopic.BoardTitle)
            {
                var board = await _boardService.GetBoard(updateTopic.BoardTitle, false);
                _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);
                newBoardId = board.Id;
                await _unreadCountersRepository.ChangeParentAsync(oldTopic.Board.Id, UnreadEntryType.Message, board.Id);
            }
        }
        else
        {
            // Non-admin users cannot change these fields
            updateTopic.IsClosed = null;
            updateTopic.IsAttached = null;
        }

        var bodyText = updateTopic.Text;
        // Topic bodies render on the Comment surface where [mod] is a green mod
        // block; strip it when the editor is a non-moderator.
        if (!string.IsNullOrEmpty(bodyText))
            bodyText = ModBlockSanitizer.SanitizeForAuthor(bodyText, _identityProvider.Current.User.Role);

        var updateEntity = new UpdateTopicEntity
        {
            TopicId = updateTopic.TopicId,
            Title = updateTopic.Title,
            Text = bodyText,
            IsClosed = updateTopic.IsClosed,
            IsAttached = updateTopic.IsAttached
        };
        var topic = await _repository.Update(updateEntity, newBoardId);
        await _invokedEventProducer.SendAsync(EventType.ChangedTopic, topic.Id);

        return topic;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await GetAsync(topicId, ct);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Board);

        await _repository.Delete(topicId);
        await _unreadCountersRepository.DeleteAsync(topicId, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.DeletedTopic, topicId);
    }

    /// <inheritdoc />
    public async Task ReorderPinnedAsync(string boardTitle, IReadOnlyList<Guid> topicIds, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, board);

        // Create order map: first topic in list = order 0
        var orderMap = topicIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        await _repository.UpdateAttachOrder(orderMap, ct);
    }
}
