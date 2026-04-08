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

        IEnumerable<Guid> validTagIds;
        if (createGame.Tags != null && createGame.Tags.Any())
        {
            var availableTags = (await _dataResolver.GetAvailableTagIds()).ToHashSet();
            validTagIds = createGame.Tags.Where(availableTags.Contains).ToList();
        }
        else
        {
            validTagIds = Enumerable.Empty<Guid>();
        }

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
            HideTemper = createGame.HideTemper,
            HideSkills = createGame.HideSkills,
            HideInventory = createGame.HideInventory,
            HideStory = createGame.HideStory,
            DisableAlignment = createGame.DisableAlignment,
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
        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        // Only cache simple anonymous queries (no search, no complex filters)
        var canCache = isAnonymous
            && query.Skip == 0
            && string.IsNullOrEmpty(query.Search)
            && query.RequiredTags == null
            && query.OptionalTags == null
            && query.ExcludedTags == null
            && (query.OwnerUsernames == null || query.OwnerUsernames.Count == 0)
            && string.IsNullOrEmpty(query.PlayerUsername);

        if (canCache)
        {
            var statusPart = query.Statuses is { Count: > 0 } ? string.Join("_", query.Statuses) : "all";
            var recruitingPart = query.RecruitmentFilter.HasValue ? $"_recruiting_{query.RecruitmentFilter.Value}" : "";
            var closedReasonPart = query.ClosedReasonFilter.HasValue ? $"_closedReason_{query.ClosedReasonFilter.Value}" : "";
            var sortPart = !string.IsNullOrEmpty(query.SortBy) ? $"_sort_{query.SortBy}_{query.SortOrder ?? "desc"}" : "";
            var cacheKey = $"{GamesByStatusCacheKeyPrefix}{statusPart}{recruitingPart}{closedReasonPart}{sortPart}";
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
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    public async Task<Game> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameByPublicId(publicId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
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
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
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
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
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

        var games = (await _repository.GetByIds(gameIdList, currentUserId)).ToArray();
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

        if (updateGame.Status.HasValue && updateGame.Status != game.Status)
        {
            var (intention, eventType) = _intentionConverter.Convert(updateGame.Status.Value);
            if (_intentionManager.IsAllowed(intention, game))
            {
                invokedEvents.Add(eventType);

                if (!game.ActivatedUtc.HasValue && updateGame.Status == ModuleStatus.Active)
                {
                    updateGame.ActivatedUtc = _dateTimeProvider.Now;
                }

                if (updateGame.Status == ModuleStatus.Closed)
                {
                    updateGame.ClosedUtc = _dateTimeProvider.Now;
                    updateGame.IsRecruitmentOpen = false;
                }

                if (game.Status == ModuleStatus.Closed && updateGame.Status != ModuleStatus.Closed)
                {
                    updateGame.ClosedUtc = null;
                    updateGame.ClosedReason = ClosedReason.None;
                }
            }
            else
            {
                updateGame.Status = null;
            }
        }

        if (updateGame.PremoderationStatus.HasValue && updateGame.PremoderationStatus != game.PremoderationStatus)
        {
            if (_intentionManager.IsAllowed(GameIntention.SetStatusModeration, game))
            {
                if (updateGame.PremoderationStatus == PremoderationStatus.Approved ||
                    updateGame.PremoderationStatus == PremoderationStatus.AwaitingEdits)
                {
                    updateGame.MentorId = _identityProvider.Current.User.UserId;
                }

                if (updateGame.PremoderationStatus == PremoderationStatus.Approved &&
                    game.PremoderationStatus != PremoderationStatus.Approved)
                {
                    updateGame.MentorId = null;
                }
            }
            else
            {
                updateGame.PremoderationStatus = null;
            }
        }

        // Check if recruitment is being opened (was closed, now opening)
        var isOpeningRecruitment = !game.Recruitment.IsOpen &&
                                   updateGame.IsRecruitmentOpen == true;

        var updateEntity = new UpdateGameEntity
        {
            GameId = updateGame.GameId,
            Status = updateGame.Status,
            PremoderationStatus = updateGame.PremoderationStatus,
            ClosedReason = updateGame.ClosedReason,
            IsRecruitmentOpen = updateGame.IsRecruitmentOpen,
            IncrementRecruitmentCount = isOpeningRecruitment,
            RecruitmentPcLimit = updateGame.RecruitmentPcLimit,
            Title = updateGame.Title,
            SystemName = updateGame.SystemName,
            NarrativeSetting = updateGame.NarrativeSetting,
            Info = updateGame.Info,
            HideTemper = updateGame.HideTemper,
            HideSkills = updateGame.HideSkills,
            HideInventory = updateGame.HideInventory,
            HideStory = updateGame.HideStory,
            DisableAlignment = updateGame.DisableAlignment,
            HideDiceResult = updateGame.HideDiceResult,
            ShowPrivateMessages = updateGame.ShowPrivateMessages,
            HidePostStats = updateGame.HidePostStats,
            CommentsAccessMode = updateGame.CommentsAccessMode,
            TagIds = updateGame.Tags,
            UpdatedUtc = _dateTimeProvider.Now,
            ActivatedUtc = updateGame.ActivatedUtc,
            ClosedUtc = updateGame.ClosedUtc,
            ClearClosedUtc = updateGame.ClosedUtc == null && game.ClosedUtc.HasValue
        };

        var result = await _repository.Update(updateEntity);

        await _producer.SendAsync(invokedEvents, game.Id);

        return result;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid gameId)
    {
        var gameToRemove = await GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Delete, gameToRemove);
        await _repository.Delete(gameId);
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
            throw new HttpException(HttpStatusCode.NotFound, "Assistant not found in this game");
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
            throw new HttpException(HttpStatusCode.Forbidden, "Cannot leave own game");
        }

        var isReader = await _userRepository.IsReader(userId, gameId);
        var isAssistant = await _userRepository.IsAssistantByUserId(userId, gameId);
        var charactersLeft = await _userRepository.MarkCharactersAsLeft(userId, gameId);

        // Check if user is a member of the game
        if (!isReader && !isAssistant && charactersLeft == 0)
        {
            throw new HttpException(HttpStatusCode.Conflict, "User is not a member of this game");
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
            query.Participating?.ToString() ?? "",
            query.CreatedFrom?.ToString("O") ?? "",
            query.CreatedTo?.ToString("O") ?? "",
            query.ActivatedFrom?.ToString("O") ?? "",
            query.ActivatedTo?.ToString("O") ?? "",
            query.ClosedFrom?.ToString("O") ?? "",
            query.ClosedTo?.ToString("O") ?? ""
        };
        return string.Join("|", parts).GetHashCode().ToString();
    }

    #endregion
}
