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
    private readonly IEventProducer _invokedEventProducer;
    private readonly IGuidFactory _guidFactory;
    private readonly ICache _cache;
    private readonly IBbTagNameCase _tagNames;

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
        IGuidFactory guidFactory,
        ICache cache,
        IBbTagNameCase tagNames)
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
        _guidFactory = guidFactory;
        _cache = cache;
        _tagNames = tagNames;
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
        // MayCreate's question, so the button the page offers and this refusal
        // cannot part.
        _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);

        var author = _identityProvider.Current.User;
        var topicId = _guidFactory.Create();
        var createEntity = new CreateTopicEntity
        {
            TopicId = topicId,
            Title = createTopic.Title,
            // Topic bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createTopic.Text, author.Role), BbTagSet.WithModBlock)
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

        await _invokedEventProducer.SendAsync(EventType.NewTopic, topic.Id);

        // The cacheable-boards listing fast path uses a short TTL
        // (CachePolicy.Medium = 1 min) so a fresh topic becomes visible
        // within 60 seconds with no explicit invalidation. This is
        // acceptable for the news board — topics are rare, staleness
        // bounded, and explicit invalidation would require iterating
        // every (take, accessPolicy) cache key combination.
        return topic;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The one gate CreateAsync is refused by once the board is found: the
    /// board's policy against the role and the ban are both in it
    /// (BoardIntentionResolver.CreateTopic).
    /// </remarks>
    public bool MayCreate(Board board) =>
        _intentionManager.IsAllowed(ForumIntention.CreateTopic, board);

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
                "Топик не найден");
        }

        await FillUnreadCounter(topic, identity);

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

        await FillUnreadCounters(topics, identity);

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

        await FillUnreadCounters(topics, identity);

        return (topics, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<Topic?> GetBestUserTopicAsync(string username, CancellationToken ct = default)
    {
        // Resolve username → UserId via the cross-module lookup so the
        // repository stays typed on Guid. Throws HttpException(404) on an
        // unknown user, which reaches the API caller unchanged.
        var user = await _userLookupService.GetAsync(username);

        // Scope to boards the current viewer can see — the same access-policy
        // mask the cross-board listing uses, so the widget never surfaces a
        // topic on a board the viewer lacks access to.
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
        var topic = await _repository.GetBestUserTopic(user.UserId, accessPolicy, ct);

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
    /// Unread counts are always filled per request and never cached: they depend
    /// on the specific viewer's read state. An anonymous viewer has no read state
    /// at all, so the total stands in for it - that is what an unread badge means
    /// to somebody who has read nothing.
    ///
    /// Written once for a single topic and once for a page of them, because five
    /// copies of one if/else is five chances to answer an anonymous reader with a
    /// zero.
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
    /// A listing query is cache-friendly when it has no dynamic filters
    /// and uses the default page size (or smaller). Search, author filters,
    /// and date ranges would blow up the cache key space; attached-only
    /// queries skip the normal paging path anyway.
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

        // Closing and reopening belong to the author as well as to the board's
        // moderators. A topic the board's moderators closed is shut to its author's
        // edits (one the author closed stays theirs), and reopening it is what opens
        // them again, so a request that reopens the topic is not refused for the
        // topic being closed: whoever may reopen a topic may edit it once it is open
        // (TopicIntentionResolverShould holds that).
        var changesClosed = updateTopic.IsClosed.HasValue && updateTopic.IsClosed != oldTopic.IsClosed;
        if (changesClosed)
        {
            _intentionManager.ThrowIfForbidden(TopicIntention.Close, oldTopic);
        }

        if (!changesClosed || updateTopic.IsClosed == true)
        {
            _intentionManager.ThrowIfForbidden(TopicIntention.Edit, oldTopic);
        }

        Guid? newBoardId = null;

        if (_intentionManager.IsAllowed(ForumIntention.AdministrateTopics, oldTopic.Board))
        {
            if (updateTopic.BoardTitle != default &&
                oldTopic.Board.Title != updateTopic.BoardTitle)
            {
                var board = await _boardService.GetBoard(updateTopic.BoardTitle, false);
                _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);
                newBoardId = board.Id;
                await _unreadCountersRepository.ChangeParentAsync(oldTopic.Id, UnreadEntryType.Message, board.Id);
            }
        }
        else
        {
            // Asking for one of these is refused; round-tripping the value the topic
            // already has is not. Both used to be dropped in silence, so a user
            // pinning somebody else's topic got 200 and an unpinned topic back, and a
            // user moving their own topic to another board got 200 and the topic
            // still in the old one -- the move is reachable from HTTP through
            // UpdateTopicRequest.Board. The closed state was checked above.
            if ((updateTopic.IsAttached.HasValue && updateTopic.IsAttached != oldTopic.IsAttached) ||
                (updateTopic.BoardTitle != default && oldTopic.Board.Title != updateTopic.BoardTitle))
            {
                _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, oldTopic.Board);
            }

            updateTopic.IsAttached = null;
        }

        var bodyText = updateTopic.Text;
        // Topic bodies render on the Comment surface where [mod] is a green mod
        // block; strip it when the editor is a non-moderator.
        if (!string.IsNullOrEmpty(bodyText))
            bodyText = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(bodyText, _identityProvider.Current.User.Role), BbTagSet.WithModBlock);

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
                ? updateTopic.IsClosed == true && oldTopic.Author?.UserId == _identityProvider.Current.User.UserId
                : null,
            IsAttached = updateTopic.IsAttached,
            // The editor is not necessarily the author: TopicIntention.Edit is
            // open to moderators and administrators too. The ChangedTopic
            // notification takes its actor from this history, so a topic changed
            // by somebody a subscriber has blocked stops being delivered.
            EditorUserId = _identityProvider.Current.User.UserId
        };
        var updated = await _repository.Update(updateEntity, newBoardId);

        // Announced only when the row moved. A PATCH that hands back the values the
        // topic already holds is answered, but there is nothing to tell subscribers
        // about — and the notification's actor is read from the edit history, which
        // such a request leaves untouched, so the announcement would have named
        // whoever edited the topic last and been filtered against his blacklist.
        if (updated.Changed)
        {
            await _invokedEventProducer.SendAsync(EventType.ChangedTopic, updated.Topic.Id);
        }

        return updated.Topic;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await GetAsync(topicId, ct);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Board);

        await _repository.Delete(topicId, _identityProvider.Current.User.UserId);
        await _unreadCountersRepository.DeleteAsync(topicId, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.DeletedTopic, topicId);
    }

    /// <inheritdoc />
    public async Task ReorderPinnedAsync(string boardTitle, IReadOnlyList<Guid> topicIds, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, board);

        // What this call replaces is the order of the board as a whole, so the
        // body has to name every pinned topic of that board and name each of them
        // once. A body naming a subset would leave the topics it skipped on the
        // positions this request has just handed to others: two topics on one
        // place, one of them moved by a request that never mentioned it.
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
}
