using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Subscriptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Game = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Unified service for game CRUD operations
/// </summary>
internal class GameService : IGameService
{
    private readonly IValidator<GamesQuery> _gamesQueryValidator;
    private readonly IValidator<UpdateGame> _updateGameValidator;
    private readonly IGameCreationValidator _creationValidator;
    private readonly IGameCreationDataResolver _dataResolver;
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaService _schemaService;
    private readonly IGameRepository _repository;
    private readonly IGameUserRepository _userRepository;
    private readonly IGameInvitationService _invitationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IGameBlacklistRepository _gameBlacklistRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IRoomRepository _roomRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IGameIntentionConverter _intentionConverter;
    private readonly IEventProducer _producer;
    private readonly ICache _cache;
    private readonly ILogger<GameService> _logger;

    private const string TagListCacheKey = nameof(TagListCacheKey);
    private const string GamesByStatusCacheKeyPrefix = "GamesByStatus_";

    public GameService(
        IValidator<GamesQuery> gamesQueryValidator,
        IValidator<UpdateGame> updateGameValidator,
        IGameCreationValidator creationValidator,
        IGameCreationDataResolver dataResolver,
        IIntentionManager intentionManager,
        IAttributeSchemaService schemaService,
        IGameRepository repository,
        IGameUserRepository userRepository,
        IGameInvitationService invitationService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker userBlacklistChecker,
        IGameBlacklistRepository gameBlacklistRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IGameSubscriptionService subscriptionService,
        IRoomRepository roomRepository,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IGameIntentionConverter intentionConverter,
        IEventProducer producer,
        ICache cache,
        ILogger<GameService> logger)
    {
        _gamesQueryValidator = gamesQueryValidator;
        _updateGameValidator = updateGameValidator;
        _creationValidator = creationValidator;
        _dataResolver = dataResolver;
        _intentionManager = intentionManager;
        _schemaService = schemaService;
        _repository = repository;
        _userRepository = userRepository;
        _invitationService = invitationService;
        _identityProvider = identityProvider;
        _userBlacklistChecker = userBlacklistChecker;
        _gameBlacklistRepository = gameBlacklistRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _subscriptionService = subscriptionService;
        _roomRepository = roomRepository;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _intentionConverter = intentionConverter;
        _producer = producer;
        _cache = cache;
        _logger = logger;
    }

    #region Create

    public async Task<GameDetails> CreateAsync(CreateGame createGame)
    {
        _logger.LogDebug("Creating game. Title={Title}", createGame.Title);

        await _creationValidator.ValidateAndAuthorize(createGame);
        var userId = _identityProvider.Current.User.UserId;

        // The master picks tags by short id — the alias the tag list serves and
        // the game filters take. The link table keys on the tags' own
        // identifiers, so the translation happens where the catalog is read.
        var validTagIds = await _dataResolver.ResolveTagIds(createGame.Tags);

        if (createGame.AttributeSchemaId.HasValue)
        {
            var allowedSchemaId = await _dataResolver.GetAllowedSchemaId(createGame.AttributeSchemaId.Value);
            createGame.AttributeSchemaId = allowedSchemaId;
        }

        var now = _dateTimeProvider.Now;
        var gameId = _guidFactory.Create();
        var roomId = _guidFactory.Create();

        var createGameEntity = new CreateGameEntity
        {
            GameId = gameId,
            MasterId = userId,
            Title = createGame.Title,
            SystemName = createGame.SystemName,
            NarrativeSetting = createGame.NarrativeSetting,
            Info = createGame.Info,
            Status = createGame.Draft ? ModuleStatus.Draft : ModuleStatus.Active,
            DraftVisibility = createGame.DraftVisibility,
            ActivatedUtc = createGame.Draft ? null : now,
            HideDiceResult = createGame.HideDiceResult,
            ShowPrivateMessages = createGame.ShowPrivateMessages,
            HidePostStats = createGame.HidePostStats,
            CommentsAccessMode = createGame.CommentsAccessMode,
            AttributeSchemaId = createGame.AttributeSchemaId,
            IsRecruitmentOpen = !createGame.Draft,
            RecruitmentStartedUtc = createGame.Draft ? null : now,
            RecruitmentCount = createGame.Draft ? 0 : 1,
            TagIds = validTagIds,
            CreatedUtc = now
        };

        var createRoomEntity = new CreateRoomEntity
        {
            RoomId = roomId,
            GameId = gameId,
            Title = "Игровая",
            Type = RoomType.Default,
            AccessType = RoomAccessType.Open,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
            OrderNumber = 1
        };

        // The counters go in first, and are undone if the game does not.
        //
        // There is no transaction across PostgreSQL and MongoDB and no outbox —
        // DATA_STORAGE.md says both in as many words — so what a feature living
        // in two stores owes is an explicit order: what is written first, and who
        // clears the remainder. Written after the insert, a failed Mongo call
        // left a committed game whose unread counters do not exist and never
        // will: nothing recreates them, and the badge of that game reads zero for
        // everybody forever. Written first, the same failure loses a game nobody
        // has seen yet, and the caller may simply try again.
        //
        // The identifiers are ours already, generated above, so this needs no
        // round trip to learn them.
        await InitializeCountersAsync(gameId, roomId);

        GameDetails createdGame;
        try
        {
            createdGame = await _repository.Create(createGameEntity, createRoomEntity);
        }
        catch
        {
            // Compensation, by the mechanism the collection already has: the
            // markers are stamped removed and the expiry index collects them.
            await _unreadCountersRepository.DeleteAsync(roomId, UnreadEntryType.Message);
            await _unreadCountersRepository.DeleteAsync(gameId, UnreadEntryType.Message);
            await _unreadCountersRepository.DeleteAsync(gameId, UnreadEntryType.Character);
            throw;
        }

        if (!string.IsNullOrEmpty(createGame.AssistantUsername))
        {
            try
            {
                await _invitationService.InviteAssistant(createdGame.Id, createGame.AssistantUsername);
            }
            catch
            {
                _logger.LogWarning("Failed to invite assistant {Username} for game {GameId}",
                    createGame.AssistantUsername, createdGame.Id);
            }
        }

        // One statement instead of a round trip per blocked user, the way the
        // blog side already copies the same list.
        if (createGame.CopyBlacklist)
        {
            var copied = await _gameBlacklistRepository.CopyFromPersonalBlacklist(createdGame.Id, userId);
            _logger.LogDebug("Copied personal blacklist to game blacklist with {Count} users", copied);
        }

        // The game is committed by now, so nothing below it may turn a created
        // game into a 500: the caller would try again and end up with two. A lost
        // event costs the subscribers one notification, which SYSTEM.md allows —
        // an event is not the carrier of the fact.
        try
        {
            await PublishGameCreatedAsync(createdGame.Id);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to announce the new game {GameId}", createdGame.Id);
        }

        _logger.LogInformation("Game created successfully. GameId={GameId}, Title={Title}, MasterId={MasterId}",
            createdGame.Id, createGame.Title, userId);

        return createdGame;
    }

    #endregion

    #region Read

    // Not CachePolicy.Permanent: the entry is not the tag catalog alone, it carries
    // the number of active games per tag, and that number moves with every game
    // created, retagged, activated or closed. The manual invalidation Permanent
    // asks for could not hold it either - this cache lives in the process, so a
    // game created on one instance would leave the counters of the rest stale.
    public Task<IEnumerable<GameTag>> GetTagsAsync() =>
        _cache.GetOrCreateAsync(TagListCacheKey, () => _repository.GetTags(), CachePolicy.Medium);

    public async Task<(IEnumerable<Game> games, PagingResult paging)> GetGamesAsync(GamesQuery query)
    {
        await _gamesQueryValidator.ValidateAndThrowAsync(query);

        // Premoderation filter is a moderation worklist capability (Mentor+),
        // guarded by the same targetless intention as taking a game into
        // premoderation. Without this gate the public list would become an
        // enumeration channel for games hidden by the Read intention.
        if (query.PremoderationStatuses is { Count: > 0 })
        {
            _intentionManager.ThrowIfForbidden(GameIntention.SetStatusModeration);
        }

        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        // Only cache simple anonymous queries (no search, no complex filters, no date ranges)
        var canCache = isAnonymous
            && query.Skip == 0
            && string.IsNullOrEmpty(query.Search)
            && query.RequiredTags == null
            && query.OptionalTags == null
            && query.ExcludedTags == null
            && (query.OwnerUsernames == null || query.OwnerUsernames.Count == 0)
            && string.IsNullOrEmpty(query.PlayerUsername)
            && query.Participating != true
            && !query.CreatedFromUtc.HasValue && !query.CreatedToUtc.HasValue
            && !query.ActivatedFromUtc.HasValue && !query.ActivatedToUtc.HasValue
            && !query.ClosedFromUtc.HasValue && !query.ClosedToUtc.HasValue
            && !query.RecruitmentStartedFromUtc.HasValue && !query.RecruitmentStartedToUtc.HasValue;

        if (canCache)
        {
            var statusPart = query.Statuses is { Count: > 0 } ? string.Join("_", query.Statuses) : "all";
            var recruitingPart = query.RecruitmentFilter.HasValue ? $"_recruiting_{query.RecruitmentFilter.Value}" : "";
            var closedReasonPart = query.ClosedReasonFilter.HasValue ? $"_closedReason_{query.ClosedReasonFilter.Value}" : "";
            var sortPart = !string.IsNullOrEmpty(query.SortBy) ? $"_sort_{query.SortBy}_{query.SortOrder ?? "desc"}" : "";
            var takePart = $"_take_{query.Take}";
            var cacheKey = $"{GamesByStatusCacheKeyPrefix}{statusPart}{recruitingPart}{closedReasonPart}{sortPart}{takePart}";
            var cached = await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                var totalCount = await _repository.Count(query, Guid.Empty);
                var pagingData = new PagingData(query, pageSize, totalCount);
                var gamesList = (await _repository.GetGames(pagingData, query, Guid.Empty)).ToArray();
                if (gamesList.Length > 0)
                {
                    var ids = gamesList.Select(g => g.Id).ToArray();
                    var postCounts = await _repository.GetTotalPostCounts(ids);
                    var commentCounts = await _repository.GetTotalCommentCounts(ids);
                    foreach (var game in gamesList)
                    {
                        game.UnreadPostsCount = postCounts.TryGetValue(game.Id, out var pc) ? pc : 0;
                        game.UnreadCommentsCount = commentCounts.TryGetValue(game.Id, out var cc) ? cc : 0;
                    }
                }
                return (games: gamesList, paging: pagingData.Result);
            }, CachePolicy.Medium);
            return cached;
        }

        // Cache base data for authenticated users (short TTL, unread counters always fresh)
        var queryKey = GetQueryKey(query);
        var authCacheKey = $"AuthGames_{currentUserId}_{queryKey}_{pageSize}";
        var (games, pagingDataAuth) = await _cache.GetOrCreateAsync(authCacheKey, async () =>
        {
            var totalCountAuth = await _repository.Count(query, currentUserId);
            var paging = new PagingData(query, pageSize, totalCountAuth);
            var gamesList = (await _repository.GetGames(paging, query, currentUserId)).ToArray();
            return (games: gamesList, paging);
        }, CachePolicy.VeryShort);

        if (games.Length == 0)
        {
            return (games, pagingDataAuth.Result);
        }

        var gameIds = games.Select(g => g.Id).ToArray();

        // Anonymous users: show total counts (they can't mark anything as read)
        if (isAnonymous)
        {
            var postCounts = await _repository.GetTotalPostCounts(gameIds);
            var commentCounts = await _repository.GetTotalCommentCounts(gameIds);
            foreach (var game in games)
            {
                game.UnreadPostsCount = postCounts.TryGetValue(game.Id, out var pc) ? pc : 0;
                game.UnreadCommentsCount = commentCounts.TryGetValue(game.Id, out var cc) ? cc : 0;
            }
            return (games, pagingDataAuth.Result);
        }

        // Authenticated users: show actual unread counts
        await _unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        var gamesWithAvailableComments = games
            .Where(g => _intentionManager.IsAllowed(GameIntention.ReadComments, g))
            .ToArray();
        if (gamesWithAvailableComments.Length > 0)
        {
            await _unreadCountersRepository.FillEntityCounters(gamesWithAvailableComments, currentUserId,
                g => g.Id, g => g.UnreadCommentsCount);
        }

        // Identifiers, which is all the sum below reads. The method this replaces
        // returned whole room rows with their pendencies and two whole users per
        // pendency, and the caller discarded every one of them.
        var gameRooms = await _repository.GetAvailableRoomIds(gameIds, currentUserId);
        var allRoomIds = gameRooms.SelectMany(r => r.Value).ToArray();

        if (allRoomIds.Length > 0)
        {
            var unreadPostCounters = await _unreadCountersRepository.SelectByEntitiesAsync(
                currentUserId, UnreadEntryType.Message, allRoomIds);
            foreach (var game in games)
            {
                if (!gameRooms.TryGetValue(game.Id, out var roomIds)) continue;
                var gameRoomIds = roomIds.ToArray();
                game.UnreadPostsCount = gameRoomIds.Sum(id =>
                    unreadPostCounters.TryGetValue(id, out var count) ? count : 0);
            }
        }

        return (games, pagingDataAuth.Result);
    }

    public async Task<Game> GetAsync(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGame(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    public async Task<Guid> ResolveIdByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var gameId = await _repository.FindGameIdByPublicId(publicId, currentUserId);

        // Same answer as the aggregate read gives for an id that addresses
        // nothing visible, so a caller cannot tell which path it took.
        return gameId ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
    }

    public async Task<Game> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameByPublicId(publicId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    public async Task<GameDetails> GetDetailsAsync(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameDetails(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        if (game.AttributeSchemaId.HasValue)
        {
            game.AttributeSchema = await _schemaService.GetAsync(game.AttributeSchemaId.Value);
        }

        game.Subscribers = await _subscriptionService.GetSubscribersAsync(gameId);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    public async Task<GameDetails> GetDetailsByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameDetailsByPublicId(publicId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        if (game.AttributeSchemaId.HasValue)
        {
            game.AttributeSchema = await _schemaService.GetAsync(game.AttributeSchemaId.Value);
        }

        game.Subscribers = await _subscriptionService.GetSubscribersAsync(game.Id);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    public async Task<IEnumerable<Game>> GetSubscribedAsync(IEnumerable<Guid> gameIds)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var gameIdList = gameIds.ToArray();
        if (gameIdList.Length == 0)
        {
            return Array.Empty<Game>();
        }

        var games = (await _repository.GetByIds(gameIdList, currentUserId, currentUserId)).ToArray();
        if (games.Length > 0)
        {
            await _unreadCountersRepository.FillEntityCounters(games, currentUserId,
                g => g.Id, g => g.UnreadCommentsCount);
        }

        return games;
    }

    #endregion

    #region Update

    public async Task<GameDetails> UpdateAsync(UpdateGame updateGame)
    {
        await _updateGameValidator.ValidateAndThrowAsync(updateGame);
        var game = await GetDetailsAsync(updateGame.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        if (updateGame.AssistantUsername != default)
        {
            var isExistingAssistant = game.Assistants
                .Any(a => a.Username.Equals(updateGame.AssistantUsername, StringComparison.InvariantCultureIgnoreCase));
            var isPendingAssistant = game.PendingAssistant?.Username
                .Equals(updateGame.AssistantUsername, StringComparison.InvariantCultureIgnoreCase) == true;

            if (!isExistingAssistant && !isPendingAssistant)
            {
                try
                {
                    await _invitationService.InviteAssistant(game.Id, updateGame.AssistantUsername);
                }
                catch (Exception exception)
                {
                    // The two expected reasons — no such user, or one the game has
                    // blacklisted — are why the invitation does not fail the update.
                    // The exception is logged whole because this catches every kind,
                    // including a store that is down, and the same swallow with no
                    // record left nothing to look at afterwards. Same shape as
                    // CreateAsync, which invites the assistant the same way.
                    _logger.LogWarning(exception,
                        "Failed to invite assistant {Username} for game {GameId}",
                        updateGame.AssistantUsername, game.Id);
                }
            }
        }

        var invokedEvents = new List<EventType> { EventType.ChangedGame };

        // No status and no premoderation status here: a game moves through them by
        // ChangeStatusAsync and ChangePremoderationAsync, which name the transition
        // and refuse an illegal one with 400 and a forbidden one with 403. This path
        // used to accept both fields, ask IsAllowed and, on a no, drop the field and
        // answer 200 with the game unchanged - one product transition through two
        // doors with different rules, and the quiet door was indistinguishable from
        // success. The premoderation arm was unreachable on top of that: it required
        // a target state its only legal transition never starts from.

        // Check if recruitment is being opened (was closed, now opening)
        var isOpeningRecruitment = !game.Recruitment.IsOpen &&
                                   updateGame.IsRecruitmentOpen == true;

        // Opening recruitment is not a status transition, so no status event
        // carries it: this is the only thing that reaches the people who asked to
        // hear when a game starts looking for players.
        if (isOpeningRecruitment)
        {
            invokedEvents.Add(EventType.GameRecruitmentOpened);
        }

        var updateEntity = new UpdateGameEntity
        {
            GameId = updateGame.GameId,
            IsRecruitmentOpen = updateGame.IsRecruitmentOpen,
            IncrementRecruitmentCount = isOpeningRecruitment,
            RecruitmentPcLimit = updateGame.RecruitmentPcLimit,
            Title = updateGame.Title,
            SystemName = updateGame.SystemName,
            NarrativeSetting = updateGame.NarrativeSetting,
            Info = updateGame.Info,
            HideDiceResult = updateGame.HideDiceResult,
            ShowPrivateMessages = updateGame.ShowPrivateMessages,
            HidePostStats = updateGame.HidePostStats,
            CommentsAccessMode = updateGame.CommentsAccessMode,
            TagIds = updateGame.Tags,
            UpdatedUtc = _dateTimeProvider.Now
        };

        var result = await _repository.Update(updateEntity);

        await _producer.SendAsync(invokedEvents, game.Id);

        return result;
    }

    public async Task<GameDetails> ChangeStatusAsync(Guid gameId, ModuleStatusTransition transition)
    {
        var game = await GetDetailsAsync(gameId);
        var now = _dateTimeProvider.Now;

        // What only a game answers for: the intention that guards the move and
        // the event it publishes. Which state the move is legal from and what
        // state it produces is the machine a blog runs on too, and that lives
        // in ModuleStatusPolicy.
        var (intention, statusEvent) = transition switch
        {
            ModuleStatusTransition.Start =>
                (GameIntention.SetStatusActive, EventType.StatusGameActive),
            ModuleStatusTransition.Freeze =>
                (GameIntention.SetStatusClosed, EventType.StatusGameFrozen),
            ModuleStatusTransition.Finish =>
                (GameIntention.SetStatusClosed, EventType.StatusGameFinished),
            ModuleStatusTransition.Close =>
                (GameIntention.SetStatusClosed, EventType.StatusGameClosed),
            ModuleStatusTransition.Reopen =>
                (GameIntention.SetStatusActive, EventType.StatusGameActive),
            _ => throw new HttpException(
                HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition)
        };

        // Legality first and authorization second, as before: an illegal move
        // is answered 400 whether or not the caller could have made a legal one.
        var change = ModuleStatusPolicy.Resolve(
            transition,
            new ModuleLifecycle(game.Status, game.ClosedReason, game.ActivatedUtc, game.ClosedUtc),
            now);
        _intentionManager.ThrowIfForbidden(intention, game);

        var update = new UpdateGameEntity
        {
            GameId = gameId,
            UpdatedUtc = now,
            Status = change.Status,
            ClosedReason = change.ClosedReason,
            ClosedUtc = change.ClosedUtc,
            ClearClosedUtc = change.ClearClosedUtc,
            ActivatedUtc = change.ActivatedUtc,
            // A game that closes stops looking for players. This is the field a
            // blog has no counterpart for, and the moves that end at Closed are
            // exactly Freeze, Finish and Close.
            IsRecruitmentOpen = change.Status == ModuleStatus.Closed ? false : (bool?)null
        };

        var result = await _repository.Update(update);
        await _producer.SendAsync(new List<EventType> { EventType.ChangedGame, statusEvent }, gameId);
        return result;
    }

    public async Task<GameDetails> ChangePremoderationAsync(string id, ModulePremoderationTransition transition)
    {
        // Site-wide Mentor+ gate (parameterless intention): the role decides who
        // may move a game through premoderation, not the per-game read gate. The
        // fetch goes straight to the repository, which takes either id form and
        // skips the schema, subscriber and unread-counter reads GetDetailsAsync
        // adds and this write never uses. Admission is the same either way: the
        // repository applies the accessibility scope, so a mentor who is not the
        // assigned curator is refused here exactly as on the read path.
        _intentionManager.ThrowIfForbidden(GameIntention.SetStatusModeration);

        var currentUserId = _identityProvider.Current.User.UserId;
        var game = Guid.TryParse(id, out var guid)
            ? await _repository.GetGameDetails(guid, currentUserId)
            : await _repository.GetGameDetailsByPublicId(id, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        var gameId = game.Id;
        var change = ModulePremoderationPolicy.Resolve(
            transition, game.PremoderationStatus, currentUserId);

        var update = new UpdateGameEntity
        {
            GameId = gameId,
            UpdatedUtc = _dateTimeProvider.Now,
            PremoderationStatus = change.Status,
            MentorId = change.MentorId,
            SetMentorId = true
        };

        var result = await _repository.Update(update);
        await _producer.SendAsync(new List<EventType> { EventType.ChangedGame, EventType.StatusGameModeration }, gameId);
        return result;
    }

    public async Task<GameDetails> ResetRecruitmentDateAsync(Guid gameId)
    {
        // Admin-only action; the controller enforces the role. Read-gate still
        // applies (active games are public, so an admin can always fetch them).
        var game = await GetDetailsAsync(gameId);
        var update = new UpdateGameEntity
        {
            GameId = game.Id,
            UpdatedUtc = _dateTimeProvider.Now,
            ClearRecruitmentStartedUtc = true
        };

        var result = await _repository.Update(update);
        await _producer.SendAsync(EventType.ChangedGame, gameId);
        return result;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid gameId)
    {
        var gameToRemove = await GetDetailsAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Delete, gameToRemove);

        var currentUser = _identityProvider.Current.User;
        var isPrivilegedModerator = currentUser.Role >= UserRole.SeniorModerator;

        // Master self-delete is restricted (SeniorModerator+ bypasses it): a
        // master may only delete a game while it is still a draft, or while it
        // is barely started — fewer than 10 posts and none of them rated.
        if (!isPrivilegedModerator && gameToRemove.Master.UserId == currentUser.UserId &&
            gameToRemove.Status != ModuleStatus.Draft)
        {
            var postCounts = await _repository.GetTotalPostCounts(new[] { gameId });
            var totalPosts = postCounts.TryGetValue(gameId, out var pc) ? pc : 0;
            var hasRatedPosts = gameToRemove.PostReviewsCount > 0;
            if (totalPosts >= 10 || hasRatedPosts)
            {
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Игру уже нельзя удалить: в ней 10 или больше постов или есть оцененные посты");
            }
        }

        await _repository.Delete(gameId, _identityProvider.Current.User.UserId);
        await _producer.SendAsync(EventType.DeletedGame, gameId);
    }

    #endregion

    #region Users

    public async Task<IEnumerable<GeneralUser>> GetAssistantsAsync(Guid gameId)
    {
        await GetAsync(gameId);
        return await _userRepository.GetAssistants(gameId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// RemoveUser, not Edit. Removing an assistant has two handles — this one and
    /// GameInvitationService.RemoveUser — and they asked different questions:
    /// Edit resolves to master or assistant, so an assistant could remove a peer,
    /// while the intention written for this very action resolves to master alone
    /// and says so in its own comment. AUTHORIZATION.md describes the second
    /// answer.
    /// </remarks>
    public async Task RemoveAssistantAsync(Guid gameId, string username)
    {
        var game = await GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.RemoveUser, game);

        if (!await _userRepository.IsAssistantByUsername(gameId, username))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Помощник не найден в этой игре");
        }

        await _userRepository.RemoveAssistantByUsername(gameId, username);
        await _producer.SendAsync(EventType.ChangedGame, gameId);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initialize unread counters for game and room
    /// </summary>
    private async Task InitializeCountersAsync(Guid gameId, Guid roomId)
    {
        // The room is parented by its game, the way RoomService parents every
        // room created afterwards. Left to the one-argument overload, the first
        // room of a game was its own parent, so it alone was missing from every
        // read that sums a game's rooms.
        await _unreadCountersRepository.CreateAsync(roomId, gameId, UnreadEntryType.Message);
        await _unreadCountersRepository.CreateAsync(gameId, UnreadEntryType.Message);
        await _unreadCountersRepository.CreateAsync(gameId, UnreadEntryType.Character);
    }

    /// <summary>
    /// Publish game created event
    /// </summary>
    private Task PublishGameCreatedAsync(Guid gameId)
    {
        return _producer.SendAsync(EventType.NewGame, gameId);
    }

    /// <summary>
    /// Build the query part of a cache key
    /// </summary>
    private static string GetQueryKey(GamesQuery query)
    {
        var parts = new List<string>
        {
            query.Skip.ToString(),
            query.Take.ToString(),
            query.Search ?? "",
            query.SortBy ?? "",
            query.SortOrder ?? "",
            query.Statuses != null ? string.Join(",", query.Statuses) : "",
            query.RecruitmentFilter?.ToString() ?? "",
            query.ClosedReasonFilter?.ToString() ?? "",
            query.RequiredTags != null ? string.Join(",", query.RequiredTags) : "",
            query.OptionalTags != null ? string.Join(",", query.OptionalTags) : "",
            query.ExcludedTags != null ? string.Join(",", query.ExcludedTags) : "",
            query.OwnerUsernames != null ? string.Join(",", query.OwnerUsernames) : "",
            query.PlayerUsername ?? "",
            query.PlayerParticipation?.ToString() ?? "",
            query.Participating?.ToString() ?? "",
            query.CreatedFromUtc?.ToString("O") ?? "",
            query.CreatedToUtc?.ToString("O") ?? "",
            query.ActivatedFromUtc?.ToString("O") ?? "",
            query.ActivatedToUtc?.ToString("O") ?? "",
            query.ClosedFromUtc?.ToString("O") ?? "",
            query.ClosedToUtc?.ToString("O") ?? "",
            query.RecruitmentStartedFromUtc?.ToString("O") ?? "",
            query.RecruitmentStartedToUtc?.ToString("O") ?? "",
            query.PremoderationStatuses != null ? string.Join(",", query.PremoderationStatuses) : ""
        };
        // The key carries the values themselves: a 32-bit hash of them lets two
        // different queries of one user share a cached page. Parts are
        // length-prefixed because free text (search, usernames) may contain the
        // separator, and a plain join would leave the same collision open.
        return string.Join("|", parts.Select(p => $"{p.Length}:{p}"));
    }

    #endregion
}
