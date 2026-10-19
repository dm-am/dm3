using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Game.Features.Rooms;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Game.Features.Games;

internal partial class GameService : IGameService
{
    private readonly IValidator<GamesQuery> _gamesQueryValidator;
    private readonly IValidator<UpdateGame> _updateGameValidator;
    private readonly IValidator<CreateGame> _createGameValidator;
    private readonly IGameTagResolver _tagResolver;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameAttributeSchemaRepository _gameSchemaRepository;
    private readonly IGameRepository _repository;
    private readonly IGameAssistantRepository _assistantRepository;
    private readonly IGameInvitationService _invitationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGameBlacklistRepository _gameBlacklistRepository;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IEventProducer _producer;
    private readonly ICache _cache;
    private readonly ILogger<GameService> _logger;

    private readonly GameUnreadCounters _unreadCounters;

    private const string TagListCacheKey = nameof(TagListCacheKey);

    /// <summary>
    /// A master deletes his started game only while it has fewer posts than this.
    /// </summary>
    private const int SelfDeletePostLimit = 10;

    public GameService(
        IValidator<GamesQuery> gamesQueryValidator,
        IValidator<UpdateGame> updateGameValidator,
        IValidator<CreateGame> createGameValidator,
        IGameTagResolver tagResolver,
        IIntentionManager intentionManager,
        IGameAttributeSchemaRepository gameSchemaRepository,
        IGameRepository repository,
        IGameAssistantRepository assistantRepository,
        IGameInvitationService invitationService,
        IIdentityProvider identityProvider,
        IGameBlacklistRepository gameBlacklistRepository,
        IUserBlacklistChecker userBlacklistChecker,
        IUnreadCountersRepository unreadCountersRepository,
        IGameSubscriptionService subscriptionService,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IEventProducer producer,
        ICache cache,
        ILogger<GameService> logger)
    {
        _gamesQueryValidator = gamesQueryValidator;
        _updateGameValidator = updateGameValidator;
        _createGameValidator = createGameValidator;
        _tagResolver = tagResolver;
        _intentionManager = intentionManager;
        _gameSchemaRepository = gameSchemaRepository;
        _repository = repository;
        _assistantRepository = assistantRepository;
        _invitationService = invitationService;
        _identityProvider = identityProvider;
        _gameBlacklistRepository = gameBlacklistRepository;
        _userBlacklistChecker = userBlacklistChecker;
        _unreadCountersRepository = unreadCountersRepository;
        _subscriptionService = subscriptionService;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _producer = producer;
        _cache = cache;
        _logger = logger;
        _unreadCounters = new GameUnreadCounters(repository, unreadCountersRepository);
    }

    #region Create

    public async Task<GameDetails> CreateAsync(CreateGame createGame)
    {
        await _createGameValidator.ValidateAndThrowAsync(createGame);
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
        var author = _identityProvider.Current.User;
        var userId = author.UserId;

        // Tags come by short id; the link table keys on the tags' own identifiers.
        var validTagIds = await _tagResolver.ResolveTagIds(createGame.Tags);

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
            PremoderationStatus = ModulePremoderationPolicy.InitialStatus(author),
            DraftVisibility = createGame.DraftVisibility,
            ActivatedUtc = createGame.Draft ? null : now,
            HideDiceResult = createGame.HideDiceResult,
            ShowPrivateMessages = createGame.ShowPrivateMessages,
            HidePostStats = createGame.HidePostStats,
            CommentsAccessMode = createGame.CommentsAccessMode,
            AttributeSpecifications = createGame.AttributeSpecifications,
            IsRecruitmentOpen = !createGame.Draft,
            RecruitmentStartedUtc = createGame.Draft ? null : now,
            RecruitmentCount = createGame.Draft ? 0 : 1,
            TagIds = validTagIds,
            CreatedUtc = now
        };

        // Markers first, row second, commit right after: see UnreadCountersReservation.
        // The room is parented by its game, as RoomService parents every later room.
        await using var counters = await _unreadCountersRepository.ReserveAsync(
            UnreadMarker.UnderParent(roomId, gameId, UnreadEntryType.Message),
            UnreadMarker.SelfParented(gameId, UnreadEntryType.Message),
            UnreadMarker.SelfParented(gameId, UnreadEntryType.Character));

        var createdGame = await _repository.Create(createGameEntity, FirstRoom(roomId, gameId));
        counters.Commit();

        // Committed by now: nothing below may turn a created game into a 500, or the caller makes two.
        await CopyBlacklistOnCreation(createGame, createdGame.Id, userId);

        // After the copy, so an assistant from the master's own blacklist is refused by the game's list.
        await InviteAssistantOnCreation(createGame, createdGame.Id);

        // A lost event costs one notification, and the producer swallows a refusal itself.
        await _producer.SendAsync(EventType.NewGame, createdGame.Id);

        _logger.LogInformation("Game created successfully. GameId={GameId}, Title={Title}, MasterId={MasterId}",
            createdGame.Id, createGame.Title, userId);

        return createdGame;
    }

    private static CreateRoomEntity FirstRoom(Guid roomId, Guid gameId) => new()
    {
        RoomId = roomId,
        GameId = gameId,
        Title = "Игровая",
        Type = RoomType.Default,
        AccessType = RoomAccessType.Open,
        ViewPrivateText = false,
        ViewDiceResults = true,
        DiceEnabled = true
    };

    /// <summary>
    /// A refused invitation is logged, not thrown: the game is committed by now.
    /// </summary>
    private async Task InviteAssistantOnCreation(CreateGame createGame, Guid gameId)
    {
        if (!string.IsNullOrEmpty(createGame.AssistantUsername))
        {
            try
            {
                await _invitationService.InviteAssistant(gameId, createGame.AssistantUsername);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to invite assistant {Username} for game {GameId}",
                    createGame.AssistantUsername, gameId);
            }
        }
    }

    /// <summary>
    /// A lost copy is refilled from the blacklist screen, so a failure is logged, not thrown.
    /// </summary>
    private async Task CopyBlacklistOnCreation(CreateGame createGame, Guid gameId, Guid masterId)
    {
        try
        {
            var copyBlacklist = createGame.CopyBlacklist ??
                                (await _userBlacklistChecker.GetSettingsAsync(masterId))
                                .HasFlag(UserBlacklistSettings.AutoPopulateContentBlacklist);
            if (copyBlacklist)
            {
                await _gameBlacklistRepository.CopyFromPersonalBlacklist(gameId, masterId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to copy the personal blacklist into game {GameId}", gameId);
        }
    }

    #endregion

    #region Read

    // Not Permanent: the entry carries active games per tag, and the cache lives in one process.
    public Task<IEnumerable<GameTag>> GetTagsAsync() =>
        _cache.GetOrCreateAsync(TagListCacheKey, () => _repository.GetTags(), CachePolicy.Medium);

    public async Task<(IEnumerable<Game> games, PagingResult paging)> GetGamesAsync(GamesQuery query)
    {
        await _gamesQueryValidator.ValidateAndThrowAsync(query);

        // The premoderation filter is the moderation worklist, or the list would enumerate hidden games.
        if (query.PremoderationStatuses is { Count: > 0 })
        {
            _intentionManager.ThrowIfForbidden(GameIntention.SetStatusModeration);
        }

        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.PageSize;

        if (!isAnonymous && IsDiscoveryListing(query))
        {
            var hiddenMasters = await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                currentUserId, UserBlacklistSettings.HideGames);
            query.ExcludedMasterIds = hiddenMasters.Count > 0 ? hiddenMasters : null;
        }

        if (isAnonymous && GamesQueryCacheKey.IsCacheableAnonymousQuery(query))
        {
            var cacheKey = GamesQueryCacheKey.BuildAnonymousCacheKey(query);
            var cached = await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                var totalCount = await _repository.Count(query, Guid.Empty);
                var pagingData = new PagingData(query, pageSize, totalCount);
                var gamesList = (await _repository.GetGames(pagingData, query, Guid.Empty)).ToArray();
                if (gamesList.Length > 0)
                {
                    await _unreadCounters.FillTotals(gamesList);
                }
                return (games: gamesList, paging: pagingData.Result);
            }, CachePolicy.Medium);
            return cached;
        }

        // Short TTL; the unread counters are always read fresh.
        var queryKey = GamesQueryCacheKey.GetQueryKey(query);
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

        // A guest marks nothing as read, so he sees the totals.
        if (isAnonymous)
        {
            await _unreadCounters.FillTotals(games);
            return (games, pagingDataAuth.Result);
        }

        await _unreadCounters.FillUnread(games, currentUserId);

        return (games, pagingDataAuth.Result);
    }

    /// <summary>
    /// The storage scope cannot ask the resolver, so the intention is asked here and handed down.
    /// </summary>
    private bool MayJudgePremoderation =>
        _intentionManager.IsAllowed(GameIntention.SetStatusModeration);

    public async Task<Game> GetAsync(Guid gameId, CancellationToken ct = default)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = Readable(await _repository.GetGame(gameId, currentUserId, MayJudgePremoderation, ct));
        await _unreadCounters.FillCounters(game, currentUserId);
        return game;
    }

    public async Task<Guid> ResolveIdByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var gameId = await _repository.FindGameIdByPublicId(publicId, currentUserId, MayJudgePremoderation);

        return gameId ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
    }

    public async Task<Game> GetByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = Readable(
            await _repository.GetGameByPublicId(publicId, currentUserId, MayJudgePremoderation));
        await _unreadCounters.FillCounters(game, currentUserId);
        return game;
    }

    public async Task<GameDetails> GetDetailsAsync(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = Readable(
            await _repository.GetGameDetails(gameId, currentUserId, MayJudgePremoderation));
        await FillDetails(game, currentUserId);
        return game;
    }

    public async Task<GameDetails> GetDetailsByPublicIdAsync(string publicId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = Readable(
            await _repository.GetGameDetailsByPublicId(publicId, currentUserId, MayJudgePremoderation));
        await FillDetails(game, currentUserId);
        return game;
    }

    /// <summary>
    /// A game hidden from the caller answers the 404 of an identifier that addresses nothing.
    /// </summary>
    private T Readable<T>(T? game) where T : Game
    {
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);
        return game;
    }

    /// <summary>
    /// The written row comes back without the sheet and the subscribers the details mapping needs.
    /// </summary>
    private async Task<GameDetails> Answered(GameDetails written)
    {
        await FillDetails(written, _identityProvider.Current.User.UserId);
        return written;
    }

    /// <remarks>
    /// Throws without a sheet: an empty one would let the next save store no attributes as the truth.
    /// </remarks>
    private async Task FillDetails(GameDetails game, Guid currentUserId)
    {
        game.AttributeSchema = await _gameSchemaRepository.Get(game.Id);

        game.Subscribers = await _subscriptionService.GetSubscribersAsync(game.Id);
        await _unreadCounters.FillCounters(game, currentUserId);
    }

    #endregion

    #region Update

    public async Task<GameDetails> UpdateAsync(UpdateGame updateGame)
    {
        await _updateGameValidator.ValidateAndThrowAsync(updateGame);
        var game = await GetDetailsAsync(updateGame.GameId);
        // The settings page save: rooms, the blacklist and the roster keep Edit in their own services.
        _intentionManager.ThrowIfForbidden(GameIntention.EditSettings, game);

        var invokedEvents = new List<EventType> { EventType.ChangedGame };

        // No status here: ChangeStatusAsync and ChangePremoderationAsync name the transition.

        var isOpeningRecruitment = !game.Recruitment.IsOpen &&
                                   updateGame.IsRecruitmentOpen == true;

        // Not a status transition, so no status event carries it.
        if (isOpeningRecruitment)
        {
            invokedEvents.Add(EventType.GameRecruitmentOpened);
        }

        // Null means the tags were not touched, unlike an empty list, and keeps the group limits off.
        var tagIds = updateGame.Tags is null
            ? null
            : await _tagResolver.ResolveTagIds(updateGame.Tags);

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
            TagIds = tagIds
        };

        var result = await Answered(await _repository.Update(updateEntity));

        await _producer.SendAsync(invokedEvents, game.Id);

        return result;
    }

    public async Task<GameDetails> ResetRecruitmentDateAsync(Guid gameId)
    {
        // Admin-only; the controller enforces the role.
        var game = await GetDetailsAsync(gameId);
        var update = new UpdateGameEntity
        {
            GameId = game.Id,
            ClearRecruitmentStartedUtc = true
        };

        var result = await Answered(await _repository.Update(update));
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

        // A master deletes only a draft or a barely started game; a senior moderator bypasses this.
        if (!isPrivilegedModerator && gameToRemove.Master?.UserId == currentUser.UserId &&
            gameToRemove.Status != ModuleStatus.Draft)
        {
            var postCounts = await _repository.GetTotalPostCounts(new[] { gameId });
            var totalPosts = postCounts.TryGetValue(gameId, out var pc) ? pc : 0;
            var hasRatedPosts = gameToRemove.PostReviewsCount > 0;
            if (totalPosts >= SelfDeletePostLimit || hasRatedPosts)
            {
                throw new HttpException(HttpStatusCode.Forbidden,
                    $"Игру уже нельзя удалить: в ней {SelfDeletePostLimit} или больше постов или есть оцененные посты");
            }
        }

        await _repository.Delete(gameId, currentUser.UserId);
        await _producer.SendAsync(EventType.DeletedGame, gameId);
    }

    #endregion

    #region Users

    public async Task<IEnumerable<GeneralUser>> GetAssistantsAsync(Guid gameId)
    {
        await GetAsync(gameId);
        return await _assistantRepository.GetAssistants(gameId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// RemoveUser, not Edit: only the master removes an assistant.
    /// </remarks>
    public async Task RemoveAssistantAsync(Guid gameId, string username)
    {
        var game = await GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.RemoveUser, game);

        if (!await _assistantRepository.IsAssistantByUsername(gameId, username))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Ассистент не найден в этой игре");
        }

        await _assistantRepository.RemoveAssistantByUsername(gameId, username);
        await _producer.SendAsync(EventType.ChangedGame, gameId);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// A list the viewer browses, the one "Скрывать игры" cleans; not his own games, not a list
    /// asked by a person's name, and not the moderation worklist.
    /// </summary>
    private static bool IsDiscoveryListing(GamesQuery query) =>
        query.Participating != true &&
        query.OwnerUsernames is not { Count: > 0 } &&
        string.IsNullOrEmpty(query.PlayerUsername) &&
        query.PremoderationStatuses is not { Count: > 0 };

    #endregion
}
