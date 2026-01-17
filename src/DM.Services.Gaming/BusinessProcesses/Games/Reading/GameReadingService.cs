using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Schemas.Reading;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Services.Gaming.BusinessProcesses.Games.Reading;

/// <inheritdoc />
internal class GameReadingService : IGameReadingService
{
    private readonly IValidator<GamesQuery> validator;
    private readonly IIntentionManager intentionManager;
    private readonly ISchemaReadingService schemaReadingService;
    private readonly IGameReadingRepository repository;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IMemoryCache cache;
    private readonly IIdentityProvider identityProvider;

    private const string TagListCacheKey = nameof(TagListCacheKey);
    private const string PopularGamesCacheKey = nameof(PopularGamesCacheKey);
    private const string GamesByStatusCacheKeyPrefix = "GamesByStatus_";
    private const int PopularGamesLimit = 10;
    private static readonly TimeSpan GamesByStatusCacheDuration = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public GameReadingService(
        IValidator<GamesQuery> validator,
        IIntentionManager intentionManager,
        ISchemaReadingService schemaReadingService,
        IGameReadingRepository repository,
        IIdentityProvider identityProvider,
        IUnreadCountersRepository unreadCountersRepository,
        IMemoryCache cache)
    {
        this.validator = validator;
        this.intentionManager = intentionManager;
        this.schemaReadingService = schemaReadingService;
        this.repository = repository;
        this.unreadCountersRepository = unreadCountersRepository;
        this.cache = cache;
        this.identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GameTag>> GetTags()
    {
        return cache.GetOrCreateAsync(TagListCacheKey, async e =>
        {
            e.SlidingExpiration = TimeSpan.FromDays(1);
            return await repository.GetTags();
        });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Game>> GetOwnGames()
    {
        var currentUserId = identityProvider.Current.User.UserId;
        var games = (await repository.GetOwn(currentUserId)).ToArray();

        if (games.Length == 0)
            return games;

        // EF Core DbContext не thread-safe, запросы последовательно
        await unreadCountersRepository.FillEntityCounters(
            games, currentUserId, g => g.Id, g => g.UnreadCommentsCount);
        await unreadCountersRepository.FillEntityCounters(
            games, currentUserId, g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        var gameIds = games.Select(g => g.Id).ToArray();
        var gameRooms = await repository.GetAvailableRoomIds(gameIds, currentUserId);
        var pendingPosts = (await repository.GetPendingPosts(gameIds, currentUserId)).ToArray();

        var allRoomIds = gameRooms.SelectMany(r => r.Value).ToArray();
        var unreadPostCounters = allRoomIds.Length > 0
            ? await unreadCountersRepository.SelectByEntities(currentUserId, UnreadEntryType.Message, allRoomIds)
            : new Dictionary<Guid, int>();

        foreach (var game in games)
        {
            if (!gameRooms.TryGetValue(game.Id, out var roomIds)) continue;
            var gameRoomIds = roomIds.ToArray();
            game.UnreadPostsCount = gameRoomIds.Sum(id =>
                unreadPostCounters.TryGetValue(id, out var count) ? count : 0);
            game.Pendings = pendingPosts.Where(p => gameRoomIds.Contains(p.RoomId));
        }

        return games;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Game> games, PagingResult paging)> GetGames(GamesQuery query)
    {
        await validator.ValidateAndThrowAsync(query);

        var identity = identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        // Для анонимов кэшируем результат (без unread counters)
        if (isAnonymous && !query.TagId.HasValue && query.Number is null or 1)
        {
            var cacheKey = $"{GamesByStatusCacheKeyPrefix}{string.Join("_", query.Statuses)}";
            var cached = await cache.GetOrCreateAsync(cacheKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = GamesByStatusCacheDuration;
                var totalCount = await repository.Count(query, Guid.Empty);
                var pagingData = new PagingData(query, pageSize, totalCount);
                var gamesList = (await repository.GetGames(pagingData, query, Guid.Empty)).ToArray();
                return (games: gamesList, paging: pagingData.Result);
            });
            return cached;
        }

        // Для авторизованных — последовательные запросы (DbContext не thread-safe)
        var totalCountAuth = await repository.Count(query, currentUserId);
        var pagingDataAuth = new PagingData(query, pageSize, totalCountAuth);
        var games = (await repository.GetGames(pagingDataAuth, query, currentUserId)).ToArray();

        if (games.Length == 0)
            return (games, pagingDataAuth.Result);

        await unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        var gamesWithAvailableComments = games
            .Where(g => intentionManager.IsAllowed(GameIntention.ReadComments, g))
            .ToArray();
        if (gamesWithAvailableComments.Length > 0)
        {
            await unreadCountersRepository.FillEntityCounters(gamesWithAvailableComments, currentUserId,
                g => g.Id, g => g.UnreadCommentsCount);
        }

        return (games, pagingDataAuth.Result);
    }

    /// <inheritdoc />
    public async Task<Game> GetGame(Guid gameId)
    {
        var currentUserId = identityProvider.Current.User.UserId;
        var game = await repository.GetGame(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        await unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    /// <inheritdoc />
    public async Task<GameExtended> GetGameDetails(Guid gameId)
    {
        var currentUserId = identityProvider.Current.User.UserId;
        var game = await repository.GetGameDetails(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        if (game.AttributeSchemaId.HasValue)
        {
            game.AttributeSchema = await schemaReadingService.Get(game.AttributeSchemaId.Value);
        }

        await unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        return game;
    }

    /// <inheritdoc />
    public Task<IEnumerable<Game>> GetPopularGames()
    {
        return cache.GetOrCreateAsync(PopularGamesCacheKey, async e =>
        {
            e.SlidingExpiration = TimeSpan.FromDays(1);
            return await repository.GetPopularGames(PopularGamesLimit);
        });
    }
}