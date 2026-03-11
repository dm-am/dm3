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
using Game = DM.Domain.Game.Features.Games.GameModel;

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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IGameIntentionConverter _intentionConverter;
    private readonly IEventProducer _producer;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameService> _logger;

    private const string TagListCacheKey = nameof(TagListCacheKey);
    private const string PopularGamesCacheKey = nameof(PopularGamesCacheKey);
    private const string GamesByStatusCacheKeyPrefix = "GamesByStatus_";
    private const string OwnGamesCacheKeyPrefix = "OwnGames_";
    private const int PopularGamesLimit = 10;

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
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _intentionConverter = intentionConverter;
        _producer = producer;
        _cache = cache;
        _logger = logger;
    }

    #region Create

    public async Task<GameExtended> CreateAsync(CreateGame createGame)
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
            AuthorId = userId,
            Title = createGame.Title,
            SystemName = createGame.SystemName,
            NarrativeSetting = createGame.NarrativeSetting,
            Info = createGame.Info,
            Status = createGame.Draft ? ModuleStatus.Draft : ModuleStatus.Active,
            ReleaseDate = createGame.Draft ? null : now,
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

    public async Task<IEnumerable<GameModel>> GetOwnGamesAsync()
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var cacheKey = $"{OwnGamesCacheKeyPrefix}{currentUserId}";

        if (_cache.TryGetValue(cacheKey, out GameModel[]? cachedGames) && cachedGames != null)
        {
            return cachedGames;
        }

        var games = (await _repository.GetOwn(currentUserId)).ToArray();
        if (games.Length == 0)
        {
            _cache.Set(cacheKey, games, CachePolicy.Short);
            return games;
        }

        var gameIds = games.Select(g => g.Id).ToArray();
        var fillCommentsTask = _unreadCountersRepository.FillEntityCounters(
            games, currentUserId, g => g.Id, g => g.UnreadCommentsCount);
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(
            games, currentUserId, g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        var roomsTask = _repository.GetRoomsAndPostPendencies(gameIds, currentUserId);

        await Task.WhenAll(fillCommentsTask, fillCharactersTask, roomsTask);

        var (gameRooms, postPendencies) = await roomsTask;
        var postPendenciesArray = postPendencies.ToArray();
        var allRoomIds = gameRooms.SelectMany(r => r.Value).ToArray();
        var unreadPostCounters = allRoomIds.Length > 0
            ? await _unreadCountersRepository.SelectByEntitiesAsync(currentUserId, UnreadEntryType.Message, allRoomIds)
            : new Dictionary<Guid, int>();

        foreach (var game in games)
        {
            if (!gameRooms.TryGetValue(game.Id, out var roomIds)) continue;
            var gameRoomIds = roomIds.ToArray();
            game.UnreadPostsCount = gameRoomIds.Sum(id =>
                unreadPostCounters.TryGetValue(id, out var count) ? count : 0);
            game.Pendencies = postPendenciesArray.Where(p => gameRoomIds.Contains(p.RoomId));
        }

        _cache.Set(cacheKey, games, CachePolicy.Short);
        return games;
    }

    public async Task<(IEnumerable<GameModel> games, PagingResult paging)> GetGamesAsync(GamesQuery query)
    {
        await _gamesQueryValidator.ValidateAndThrowAsync(query);
        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        if (isAnonymous && !query.TagId.HasValue && query.Skip == 0)
        {
            var recruitingPart = query.IsRecruiting.HasValue ? $"_recruiting_{query.IsRecruiting.Value}" : "";
            var finishedPart = query.IsFinished.HasValue ? $"_finished_{query.IsFinished.Value}" : "";
            var cacheKey = $"{GamesByStatusCacheKeyPrefix}{string.Join("_", query.Statuses)}{recruitingPart}{finishedPart}";
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

        var totalCountAuth = await _repository.Count(query, currentUserId);
        var pagingDataAuth = new PagingData(query, pageSize, totalCountAuth);
        var games = (await _repository.GetGames(pagingDataAuth, query, currentUserId)).ToArray();

        if (games.Length == 0)
        {
            return (games, pagingDataAuth.Result);
        }

        var gameIds = games.Select(g => g.Id).ToArray();
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        var gamesWithAvailableComments = games
            .Where(g => _intentionManager.IsAllowed(GameIntention.ReadComments, g))
            .ToArray();
        var fillCommentsTask = gamesWithAvailableComments.Length > 0
            ? _unreadCountersRepository.FillEntityCounters(gamesWithAvailableComments, currentUserId,
                g => g.Id, g => g.UnreadCommentsCount)
            : Task.CompletedTask;
        var roomsTask = _repository.GetRoomsAndPostPendencies(gameIds, currentUserId);

        await Task.WhenAll(fillCharactersTask, fillCommentsTask, roomsTask);

        var (gameRooms, _) = await roomsTask;
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

    public async Task<GameModel> GetAsync(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGame(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        var fillCommentsTask = _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        await Task.WhenAll(fillCommentsTask, fillCharactersTask);
        return game;
    }

    public async Task<GameExtended> GetDetailsAsync(Guid gameId)
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

        var readersTask = _subscriptionService.GetReadersAsync(gameId);
        var fillCommentsTask = _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        await Task.WhenAll(readersTask, fillCommentsTask, fillCharactersTask);
        game.Readers = await readersTask;

        return game;
    }

    public async Task<IEnumerable<GameModel>> GetPopularAsync()
    {
        return (await _cache.GetOrCreateAsync(PopularGamesCacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CachePolicy.LongLived;
            var games = (await _repository.GetPopularGames(PopularGamesLimit)).ToArray();
            if (games.Length > 0)
            {
                var gameIds = games.Select(g => g.Id).ToArray();
                var postCounts = await _repository.GetTotalPostCounts(gameIds);
                var commentCounts = await _repository.GetTotalCommentCounts(gameIds);
                foreach (var game in games)
                {
                    game.UnreadPostsCount = postCounts.TryGetValue(game.Id, out var pc) ? pc : 0;
                    game.UnreadCommentsCount = commentCounts.TryGetValue(game.Id, out var cc) ? cc : 0;
                }
            }
            return games.AsEnumerable();
        }))!;
    }

    public async Task<IEnumerable<GameModel>> GetSubscribedAsync(IEnumerable<Guid> gameIds)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var gameIdList = gameIds.ToArray();
        if (gameIdList.Length == 0)
        {
            return Array.Empty<GameModel>();
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

    public async Task<GameExtended> UpdateAsync(UpdateGame updateGame)
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

                if (!game.ReleaseDate.HasValue && updateGame.Status == ModuleStatus.Active)
                {
                    updateGame.ReleaseDate = _dateTimeProvider.Now;
                }

                if (updateGame.Status == ModuleStatus.Closed)
                {
                    updateGame.ClosedUtc = _dateTimeProvider.Now;
                    updateGame.IsRecruitmentOpen = false;
                }

                if (game.Status == ModuleStatus.Closed && updateGame.Status != ModuleStatus.Closed)
                {
                    updateGame.ClosedUtc = null;
                    updateGame.IsFinished = false;
                    updateGame.IsFrozen = false;
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

        var updateEntity = new UpdateGameEntity
        {
            GameId = updateGame.GameId,
            Status = updateGame.Status,
            PremoderationStatus = updateGame.PremoderationStatus,
            IsFinished = updateGame.IsFinished,
            IsFrozen = updateGame.IsFrozen,
            IsRecruitmentOpen = updateGame.IsRecruitmentOpen,
            RecruitmentPlayerLimit = updateGame.RecruitmentPlayerLimit,
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
            UpdatedUtc = _dateTimeProvider.Now
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

    public async Task<IEnumerable<GeneralUser>> GetPlayersAsync(Guid gameId)
    {
        await GetAsync(gameId);
        return await _userRepository.GetPlayers(gameId);
    }

    public async Task RemovePlayerAsync(Guid gameId, string username)
    {
        var game = await GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        if (!await _userRepository.IsPlayer(gameId, username))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Player not found in this game");
        }

        var exiledCharacterIds = await _userRepository.ExilePlayer(gameId, username);
        await _producer.SendAsync(EventType.ChangedGame, gameId);

        foreach (var characterId in exiledCharacterIds)
        {
            await _producer.SendAsync(EventType.StatusCharacterExiled, characterId);
        }
    }

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
        if (game.Author.UserId == userId)
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

    #endregion
}
