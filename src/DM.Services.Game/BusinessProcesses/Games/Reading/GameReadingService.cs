using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.AttributeSchemas.Reading;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Services.Game.BusinessProcesses.Games.Reading;

/// <inheritdoc />
internal class GameReadingService : IGameReadingService
{
    private readonly IValidator<GamesQuery> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaReadingService _schemaReadingService;
    private readonly IGameReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IMemoryCache _cache;
    private readonly IIdentityProvider _identityProvider;

    private const string TagListCacheKey = nameof(TagListCacheKey);
    private const string PopularGamesCacheKey = nameof(PopularGamesCacheKey);
    private const string GamesByStatusCacheKeyPrefix = "GamesByStatus_";
    private const string OwnGamesCacheKeyPrefix = "OwnGames_";
    private const int PopularGamesLimit = 10;

    /// <inheritdoc />
    public GameReadingService(
        IValidator<GamesQuery> validator,
        IIntentionManager intentionManager,
        IAttributeSchemaReadingService schemaReadingService,
        IGameReadingRepository repository,
        IIdentityProvider identityProvider,
        IUnreadCountersRepository unreadCountersRepository,
        IMemoryCache cache)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _schemaReadingService = schemaReadingService;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _cache = cache;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameTag>> GetTags()
    {
        return (await _cache.GetOrCreateAsync(TagListCacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CachePolicy.Permanent;
            return await _repository.GetTags();
        }))!;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Game>> GetOwnGames()
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var cacheKey = $"{OwnGamesCacheKeyPrefix}{currentUserId}";

        // Простой кэш без race condition проблем GetOrCreateAsync
        if (_cache.TryGetValue(cacheKey, out Dto.Output.Game[]? cachedGames) && cachedGames != null)
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

        // Все запросы параллельно: MongoDB counters + PostgreSQL rooms
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
            ? await _unreadCountersRepository.SelectByEntities(currentUserId, UnreadEntryType.Message, allRoomIds)
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

    /// <inheritdoc />
    public async Task<(IEnumerable<Dto.Output.Game> games, PagingResult paging)> GetGames(GamesQuery query)
    {
        await _validator.ValidateAndThrowAsync(query);

        var identity = _identityProvider.Current;
        var currentUserId = identity.User.UserId;
        var isAnonymous = !identity.User.IsAuthenticated;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        // Для анонимов кэшируем результат с total counters
        if (isAnonymous && !query.TagId.HasValue && query.Number is null or 1)
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
                    var gameIds = gamesList.Select(g => g.Id).ToArray();

                    // Для анонимов — показываем TOTAL counts вместо unread
                    // DbContext не потокобезопасен — выполняем последовательно
                    var postCounts = await _repository.GetTotalPostCounts(gameIds);
                    var commentCounts = await _repository.GetTotalCommentCounts(gameIds);

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

        // Для авторизованных — последовательные запросы (DbContext не thread-safe)
        var totalCountAuth = await _repository.Count(query, currentUserId);
        var pagingDataAuth = new PagingData(query, pageSize, totalCountAuth);
        var games = (await _repository.GetGames(pagingDataAuth, query, currentUserId)).ToArray();

        if (games.Length == 0)
            return (games, pagingDataAuth.Result);

        var gameIds = games.Select(g => g.Id).ToArray();

        // MongoDB thread-safe, можно параллелить
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

        var gamesWithAvailableComments = games
            .Where(g => _intentionManager.IsAllowed(GameIntention.ReadComments, g))
            .ToArray();

        var fillCommentsTask = gamesWithAvailableComments.Length > 0
            ? _unreadCountersRepository.FillEntityCounters(gamesWithAvailableComments, currentUserId,
                g => g.Id, g => g.UnreadCommentsCount)
            : Task.CompletedTask;

        // PostgreSQL: get room IDs for unread posts aggregation
        var roomsTask = _repository.GetRoomsAndPostPendencies(gameIds, currentUserId);

        await Task.WhenAll(fillCharactersTask, fillCommentsTask, roomsTask);

        // Aggregate unread posts from rooms
        var (gameRooms, _) = await roomsTask;
        var allRoomIds = gameRooms.SelectMany(r => r.Value).ToArray();
        if (allRoomIds.Length > 0)
        {
            var unreadPostCounters = await _unreadCountersRepository.SelectByEntities(
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

    /// <inheritdoc />
    public async Task<Dto.Output.Game> GetGame(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGame(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        // MongoDB thread-safe, можно параллелить
        var fillCommentsTask = _unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        await Task.WhenAll(fillCommentsTask, fillCharactersTask);

        return game;
    }

    /// <inheritdoc />
    public async Task<GameExtended> GetGameDetails(Guid gameId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var game = await _repository.GetGameDetails(gameId, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Game not found");
        }

        if (game.AttributeSchemaId.HasValue)
        {
            game.AttributeSchema = await _schemaReadingService.Get(game.AttributeSchemaId.Value);
        }

        // MongoDB thread-safe, можно параллелить
        var fillCommentsTask = _unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        var fillCharactersTask = _unreadCountersRepository.FillEntityCounters(new[] {game}, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        await Task.WhenAll(fillCommentsTask, fillCharactersTask);

        return game;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Game>> GetPopularGames()
    {
        return (await _cache.GetOrCreateAsync(PopularGamesCacheKey, async e =>
        {
            // AbsoluteExpiration ensures cache refreshes even with constant access
            e.AbsoluteExpirationRelativeToNow = CachePolicy.LongLived;
            var games = (await _repository.GetPopularGames(PopularGamesLimit)).ToArray();

            if (games.Length > 0)
            {
                var gameIds = games.Select(g => g.Id).ToArray();

                // Fill total counts for popular games (shown to everyone including anonymous)
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
}