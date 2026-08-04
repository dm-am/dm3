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
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Subscriptions;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;
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
        IMemoryCache cache,
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

        var createdGame = await _repository.Create(createGameEntity, createRoomEntity);

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

        if (createGame.CopyBlacklist)
        {
            var personalBlacklist = await _userBlacklistChecker.GetBlockedUserIdsAsync(userId);
            foreach (var blockedUserId in personalBlacklist)
            {
                await _gameBlacklistRepository.Add(createdGame.Id, blockedUserId, userId);
            }
            _logger.LogDebug("Copied personal blacklist to game blacklist with {Count} users",
                personalBlacklist.Count());
        }

        var firstRoomId = createdGame.Rooms.FirstOrDefault()?.Id ?? Guid.Empty;
        await InitializeCountersAsync(createdGame.Id, firstRoomId);
        await PublishGameCreatedAsync(createdGame.Id);

        _logger.LogInformation("Game created successfully. GameId={GameId}, Title={Title}, MasterId={MasterId}",
            createdGame.Id, createGame.Title, userId);

        return createdGame;
    }

    #endregion

    #region Read

    public async Task<IEnumerable<GameTag>> GetTagsAsync()
    {
        return (await _cache.GetOrCreateAsync(TagListCacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CachePolicy.Permanent;
            return await _repository.GetTags();
        }))!;
    }

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
            var cached = await _cache.GetOrCreateAsync(cacheKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = CachePolicy.Medium;
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
            });
            return cached;
        }

        // Cache base data for authenticated users (short TTL, unread counters always fresh)
        var queryHash = GetQueryHash(query);
        var authCacheKey = $"AuthGames_{currentUserId}_{queryHash}_{pageSize}";
        var (games, pagingDataAuth) = await _cache.GetOrCreateAsync(authCacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            var totalCountAuth = await _repository.Count(query, currentUserId);
            var paging = new PagingData(query, pageSize, totalCountAuth);
            var gamesList = (await _repository.GetGames(paging, query, currentUserId)).ToArray();
            return (games: gamesList, paging);
        });

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

        var (gameRooms, _) = await _repository.GetRoomsAndPostPendencies(gameIds, currentUserId);
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
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
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
        return gameId ?? throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
    }

    public async Task<Game> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameByPublicId(publicId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
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
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
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
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
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
                catch
                {
                    // Ignore invitation errors - user might not exist or be blacklisted
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

    public async Task<GameDetails> ChangeStatusAsync(Guid gameId, GameStatusTransition transition)
    {
        var game = await GetDetailsAsync(gameId);
        var now = _dateTimeProvider.Now;
        var update = new UpdateGameEntity { GameId = gameId, UpdatedUtc = now };
        EventType statusEvent;

        switch (transition)
        {
            case GameStatusTransition.Start:
                RequireStatus(game, ModuleStatus.Draft, transition);
                _intentionManager.ThrowIfForbidden(GameIntention.SetStatusActive, game);
                update.Status = ModuleStatus.Active;
                if (!game.ActivatedUtc.HasValue) update.ActivatedUtc = now;
                statusEvent = EventType.StatusGameActive;
                break;

            case GameStatusTransition.Freeze:
                RequireStatus(game, ModuleStatus.Active, transition);
                _intentionManager.ThrowIfForbidden(GameIntention.SetStatusClosed, game);
                update.Status = ModuleStatus.Closed;
                update.ClosedReason = ClosedReason.Frozen;
                update.ClosedUtc = now;
                update.IsRecruitmentOpen = false;
                statusEvent = EventType.StatusGameFrozen;
                break;

            case GameStatusTransition.Finish:
                RequireStatus(game, ModuleStatus.Active, transition);
                _intentionManager.ThrowIfForbidden(GameIntention.SetStatusClosed, game);
                update.Status = ModuleStatus.Closed;
                update.ClosedReason = ClosedReason.Finished;
                update.ClosedUtc = now;
                update.IsRecruitmentOpen = false;
                statusEvent = EventType.StatusGameFinished;
                break;

            case GameStatusTransition.Close:
                // Active -> Closed+None, or Closed+Frozen -> Closed+None
                if (game.Status != ModuleStatus.Active &&
                    !(game.Status == ModuleStatus.Closed && game.ClosedReason == ClosedReason.Frozen))
                {
                    throw IllegalTransition(transition, game);
                }
                _intentionManager.ThrowIfForbidden(GameIntention.SetStatusClosed, game);
                update.Status = ModuleStatus.Closed;
                update.ClosedReason = ClosedReason.None;
                update.IsRecruitmentOpen = false;
                if (!game.ClosedUtc.HasValue) update.ClosedUtc = now;
                statusEvent = EventType.StatusGameClosed;
                break;

            case GameStatusTransition.Reopen:
                RequireStatus(game, ModuleStatus.Closed, transition);
                _intentionManager.ThrowIfForbidden(GameIntention.SetStatusActive, game);
                update.Status = ModuleStatus.Active;
                update.ClosedReason = ClosedReason.None;
                update.ClearClosedUtc = true;
                if (!game.ActivatedUtc.HasValue) update.ActivatedUtc = now;
                statusEvent = EventType.StatusGameActive;
                break;

            default:
                throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition);
        }

        var result = await _repository.Update(update);
        await _producer.SendAsync(new List<EventType> { EventType.ChangedGame, statusEvent }, gameId);
        return result;
    }

    public async Task<GameDetails> ChangePremoderationAsync(string id, GamePremoderationTransition transition)
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
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.GameNotFound);
        }

        var gameId = game.Id;
        var update = new UpdateGameEntity { GameId = gameId, UpdatedUtc = _dateTimeProvider.Now };

        switch (transition)
        {
            case GamePremoderationTransition.SendToPremoderation:
                if (game.PremoderationStatus != PremoderationStatus.AwaitingEdits)
                {
                    throw new HttpException(HttpStatusCode.BadRequest,
                        RefusalMessage.CannotSubmitForPremoderation(game.PremoderationStatus));
                }
                update.PremoderationStatus = PremoderationStatus.AwaitingApproval;
                update.MentorId = currentUserId;
                update.SetMentorId = true;
                break;

            case GamePremoderationTransition.RemoveFromPremoderation:
                if (game.PremoderationStatus != PremoderationStatus.AwaitingApproval)
                {
                    throw new HttpException(HttpStatusCode.BadRequest,
                        RefusalMessage.CannotWithdrawFromPremoderation(game.PremoderationStatus));
                }
                update.PremoderationStatus = PremoderationStatus.Approved;
                update.MentorId = null;
                update.SetMentorId = true;
                break;

            default:
                throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.UnknownPremoderationTransition);
        }

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

    private static void RequireStatus(Game game, ModuleStatus expected, GameStatusTransition transition)
    {
        if (game.Status != expected)
        {
            throw IllegalTransition(transition, game);
        }
    }

    private static HttpException IllegalTransition(GameStatusTransition transition, Game game) =>
        new(HttpStatusCode.BadRequest,
            $"Переход \"{transition}\" недоступен из статуса \"{game.Status}\"" +
            (game.Status == ModuleStatus.Closed ? $" ({game.ClosedReason})" : ""));

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

    public async Task RemoveAssistantAsync(Guid gameId, string username)
    {
        var game = await GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        if (!await _userRepository.IsAssistantByUsername(gameId, username))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Помощник не найден в этой игре");
        }

        await _userRepository.RemoveAssistantByUsername(gameId, username);
        await _producer.SendAsync(EventType.ChangedGame, gameId);
    }

    /// <inheritdoc />
    public async Task LeaveAsync(Guid gameId)
    {
        _intentionManager.ThrowIfForbidden(GameIntention.Subscribe);
        var game = await GetAsync(gameId);

        var userId = _identityProvider.Current.User.UserId;

        // Cannot leave own game
        if (game.Master.UserId == userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя покинуть свою игру");
        }

        var isReader = await _userRepository.IsReader(userId, gameId);
        var isAssistant = await _userRepository.IsAssistantByUserId(userId, gameId);
        var charactersLeft = await _userRepository.MarkCharactersAsLeft(userId, gameId);

        // Check if user is a member of the game
        if (!isReader && !isAssistant && charactersLeft == 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Вы не участвуете в этой игре");
        }

        // Remove reader subscription
        if (isReader)
        {
            await _userRepository.RemoveReader(userId, gameId);
        }

        // Remove assistant role
        if (isAssistant)
        {
            await _userRepository.RemoveAssistantByUserId(userId, gameId);
        }

        // Send event
        await _producer.SendAsync(EventType.ChangedGame, gameId);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initialize unread counters for game and room
    /// </summary>
    private async Task InitializeCountersAsync(Guid gameId, Guid roomId)
    {
        await _unreadCountersRepository.CreateAsync(roomId, UnreadEntryType.Message);
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
    /// Generate hash for query parameters (for cache key)
    /// </summary>
    private static string GetQueryHash(GamesQuery query)
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
        return string.Join("|", parts).GetHashCode().ToString();
    }

    #endregion
}
