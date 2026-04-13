using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Core.Identity;
using DbToken = DM.Infrastructure.Persistence.Entities.Account.Token;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using GameDto = DM.Domain.Game.Features.Games.Game;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTag = DM.Infrastructure.Persistence.Entities.Game.Links.GameTag;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Repository for game operations
/// </summary>
internal class GameRepository : IGameRepository
{
    private static readonly TimeSpan ActivePeriod = TimeSpan.FromDays(30);

    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPublicIdService _publicIdService;

    public GameRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IPublicIdService publicIdService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _publicIdService = publicIdService;
    }

    #region Read Operations

    public Task<int> Count(GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        return ApplyFilters(_dbContext.Games.AsQueryable(), query, userId)
            .CountAsync(ct);
    }

    public async Task<IEnumerable<GameDto>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        var gamesQuery = ApplyFilters(_dbContext.Games.AsQueryable(), query, userId);
        var orderedGames = ApplySorting(gamesQuery, query);

        var games = await orderedGames
            .Page(pagingData)
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, ct);
        return games;
    }

    /// <summary>
    /// Populate computed / aggregated fields on Game DTOs that cannot
    /// be produced by the ProjectTo mapping (ActiveCharacters, Players,
    /// SubscriberIds, Recruitment.PcCount, GameReviewsCount,
    /// PostReviewsCount, SubscriberUsernames, Pending* invitations).
    ///
    /// This is the single source of truth for "full game DTO hydration"
    /// and MUST be called by every read path that returns a Game or
    /// GameDetails (list, by-ids, details). Previously the inline
    /// duplicated block lived in three places; details-reads skipped
    /// it entirely, which is why the game tooltip on featured posts
    /// silently showed no characters. Works uniformly for Game and
    /// GameDetails (GameDetails : Game) — the type parameter is
    /// constrained so callers can pass paged lists, ad-hoc arrays, or
    /// a single-element array from a details fetch, all for the same
    /// code path.
    ///
    /// All queries are batched (GROUP BY / IN(...)) so the cost is
    /// O(1) per table regardless of how many games are in the input.
    /// </summary>
    private async Task EnrichGamesAsync<T>(
        IReadOnlyList<T> games,
        CancellationToken ct) where T : GameDto
    {
        if (games.Count == 0) return;
        var gameIds = games.Select(g => g.Id).ToHashSet();

        // PcCount — active non-NPC characters per game, single GROUP BY.
        var pcCounts = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc)
            .GroupBy(c => c.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        foreach (var game in games)
        {
            game.Recruitment.PcCount = pcCounts.GetValueOrDefault(game.Id, 0);
        }

        // Players — unique authors of active non-NPC characters.
        var playerData = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc && c.AuthorId.HasValue)
            .Select(c => new { c.GameId, c.Author })
            .Where(c => c.Author != null)
            .ToListAsync(ct);

        var playersMap = playerData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.Author!).DistinctBy(u => u.UserId).ToList());

        // Subscriber ids for participation detection.
        var subscriptionMap = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Game && gameIds.Contains(s.TargetId))
            .GroupBy(s => s.TargetId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SubscriberId).ToHashSet(), ct);

        foreach (var game in games)
        {
            game.Players = playersMap.TryGetValue(game.Id, out var players)
                ? players.Select(u => _mapper.Map<GeneralUser>(u)).ToList()
                : [];
            game.SubscriberIds = subscriptionMap.GetValueOrDefault(game.Id, []);
        }

        // Invitation tokens — single query instead of N subqueries.
        var tokens = await _dbContext.Tokens
            .Include(t => t.User)
            .Where(t => !t.IsRemoved &&
                        t.EntityId.HasValue &&
                        gameIds.Contains(t.EntityId.Value) &&
                        (t.Type == TokenType.GameAssistantInvitation ||
                         t.Type == TokenType.GamePlayerInvitation ||
                         t.Type == TokenType.GameReaderInvitation))
            .ToListAsync(ct);

        var tokensByGame = tokens.GroupBy(t => t.EntityId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var game in games)
        {
            if (tokensByGame.TryGetValue(game.Id, out var gameTokens))
            {
                var assistantToken = gameTokens.FirstOrDefault(t => t.Type == TokenType.GameAssistantInvitation);
                game.PendingAssistant = assistantToken != null
                    ? _mapper.Map<GeneralUser>(assistantToken.User)
                    : null;
                game.PendingInvitedUserIds = gameTokens
                    .Where(t => t.Type == TokenType.GamePlayerInvitation || t.Type == TokenType.GameReaderInvitation)
                    .Select(t => t.UserId)
                    .ToHashSet();
                game.PendingPlayerInvitedUserIds = gameTokens
                    .Where(t => t.Type == TokenType.GamePlayerInvitation)
                    .Select(t => t.UserId)
                    .ToHashSet();
            }
            else
            {
                game.PendingAssistant = null;
                game.PendingInvitedUserIds = new HashSet<Guid>();
                game.PendingPlayerInvitedUserIds = new HashSet<Guid>();
            }
        }

        // Game / post review counts — both batched by GameId.
        var gameReviewCounts = await _dbContext.GameReviews
            .Where(r => !r.IsRemoved && gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        var postReviewCounts = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);

        // Subscriber usernames for tooltip (cap at 20 per game).
        var subscriberData = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Game && gameIds.Contains(s.TargetId))
            .Select(s => new { s.TargetId, Username = s.Subscriber.Username })
            .ToListAsync(ct);

        var subscriberUsernamesMap = subscriberData
            .GroupBy(s => s.TargetId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => s.Username).Take(20).ToList());

        // Active characters for [X/Y] tooltip — feeds game/room tooltips.
        var activeCharacterData = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc)
            .Select(c => new { c.GameId, c.Name, OwnerUsername = c.Author!.Username })
            .ToListAsync(ct);

        var activeCharactersMap = activeCharacterData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new ActiveCharacterInfo
                {
                    Name = c.Name,
                    OwnerUsername = c.OwnerUsername
                }).ToList());

        foreach (var game in games)
        {
            game.GameReviewsCount = gameReviewCounts.GetValueOrDefault(game.Id, 0);
            game.PostReviewsCount = postReviewCounts.GetValueOrDefault(game.Id, 0);
            game.SubscriberUsernames = subscriberUsernamesMap.GetValueOrDefault(game.Id, []);
            game.ActiveCharacters = activeCharactersMap.GetValueOrDefault(game.Id, []);
        }
    }

    private IQueryable<DbGame> ApplyFilters(IQueryable<DbGame> games, GamesQuery query, Guid userId)
    {
        games = games.Where(GameAccessibilityFilters.GameAvailable(userId));

        // Text search with fuzzy matching (OR between title/system/setting)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchPattern = "%" + query.Search.Replace("%", "\\%").Replace("_", "\\_") + "%";
            var searchLower = query.Search.ToLower();
            games = games.Where(g =>
                // Contains match (case-insensitive)
                EF.Functions.ILike(g.Title, searchPattern) ||
                (g.SystemName != null && EF.Functions.ILike(g.SystemName, searchPattern)) ||
                (g.NarrativeSetting != null && EF.Functions.ILike(g.NarrativeSetting, searchPattern)) ||
                // Fuzzy match with trigrams (typo tolerance)
                EF.Functions.TrigramsSimilarity(g.Title, searchLower) > 0.3 ||
                (g.SystemName != null && EF.Functions.TrigramsSimilarity(g.SystemName, searchLower) > 0.3) ||
                (g.NarrativeSetting != null && EF.Functions.TrigramsSimilarity(g.NarrativeSetting, searchLower) > 0.3));
        }

        // Statuses with sub-filters (OR between statuses, sub-filters apply within each status)
        if (query.Statuses is { Count: > 0 })
        {
            var statuses = query.Statuses.ToHashSet();

            games = games.Where(g =>
                // Draft - no sub-filters
                (statuses.Contains(ModuleStatus.Draft) && g.Status == ModuleStatus.Draft)
                ||
                // Active - RecruitmentFilter sub-filter
                (statuses.Contains(ModuleStatus.Active) && g.Status == ModuleStatus.Active
                    && (query.RecruitmentFilter == null
                        || query.RecruitmentFilter == RecruitmentFilter.Any
                        || (query.RecruitmentFilter == RecruitmentFilter.Open && g.IsRecruitmentOpen)
                        || (query.RecruitmentFilter == RecruitmentFilter.Initial && g.IsRecruitmentOpen && g.RecruitmentCount == 1)
                        || (query.RecruitmentFilter == RecruitmentFilter.Subsequent && g.IsRecruitmentOpen && g.RecruitmentCount >= 2)
                        || (query.RecruitmentFilter == RecruitmentFilter.Closed && !g.IsRecruitmentOpen)))
                ||
                // Closed - ClosedReasonFilter sub-filter
                (statuses.Contains(ModuleStatus.Closed) && g.Status == ModuleStatus.Closed
                    && (query.ClosedReasonFilter == null
                        || g.ClosedReason == query.ClosedReasonFilter)));
        }

        // Required tags - game must have ALL of them (AND logic)
        if (query.RequiredTags is { Count: > 0 })
        {
            var required = query.RequiredTags.ToList();
            games = games.Where(g =>
                g.GameTags.Count(t => required.Contains(t.Tag.ShortId)) >= required.Count);
        }

        // Optional tags - game must have at least ONE of them (OR logic)
        if (query.OptionalTags is { Count: > 0 })
        {
            var optional = query.OptionalTags.ToArray();
            games = games.Where(g =>
                g.GameTags.Any(t => optional.Contains(t.Tag.ShortId)));
        }

        // Excluded tags - game must NOT have any of them (NOR logic)
        if (query.ExcludedTags is { Count: > 0 })
        {
            var excluded = query.ExcludedTags.ToArray();
            games = games.Where(g =>
                !g.GameTags.Any(t => excluded.Contains(t.Tag.ShortId)));
        }

        // Owner filter - master OR assistant (case-insensitive per USERNAME_POLICY.md, OR logic)
        if (query.OwnerUsernames is { Count: > 0 })
        {
            var usernamesLower = query.OwnerUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.ToLower())
                .ToArray();
            if (usernamesLower.Length > 0)
            {
                games = games.Where(g =>
                    usernamesLower.Contains(g.Master.Username.ToLower()) ||
                    g.Assistants.Any(a => usernamesLower.Contains(a.User.Username.ToLower())));
            }
        }

        // Date range filters
        // CreatedUtc always exists, so just filter by range
        if (query.CreatedFrom.HasValue)
        {
            games = games.Where(g => g.CreatedUtc >= query.CreatedFrom.Value);
        }
        if (query.CreatedTo.HasValue)
        {
            games = games.Where(g => g.CreatedUtc <= query.CreatedTo.Value);
        }

        // ActivatedUtc - exclude games without this date if filter is set
        if (query.ActivatedFrom.HasValue)
        {
            games = games.Where(g => g.ActivatedUtc.HasValue && g.ActivatedUtc.Value >= query.ActivatedFrom.Value);
        }
        if (query.ActivatedTo.HasValue)
        {
            games = games.Where(g => g.ActivatedUtc.HasValue && g.ActivatedUtc.Value <= query.ActivatedTo.Value);
        }

        // ClosedUtc - exclude games without this date if filter is set
        if (query.ClosedFrom.HasValue)
        {
            games = games.Where(g => g.ClosedUtc.HasValue && g.ClosedUtc.Value >= query.ClosedFrom.Value);
        }
        if (query.ClosedTo.HasValue)
        {
            games = games.Where(g => g.ClosedUtc.HasValue && g.ClosedUtc.Value <= query.ClosedTo.Value);
        }

        // RecruitmentStartedUtc - exclude games without this date if filter is set
        if (query.RecruitmentStartedFrom.HasValue)
        {
            games = games.Where(g => g.RecruitmentStartedUtc.HasValue &&
                                     g.RecruitmentStartedUtc.Value >= query.RecruitmentStartedFrom.Value);
        }
        if (query.RecruitmentStartedTo.HasValue)
        {
            games = games.Where(g => g.RecruitmentStartedUtc.HasValue &&
                                     g.RecruitmentStartedUtc.Value <= query.RecruitmentStartedTo.Value);
        }

        // Player filter (case-insensitive per USERNAME_POLICY.md)
        if (!string.IsNullOrEmpty(query.PlayerUsername))
        {
            var usernameLower = query.PlayerUsername.ToLower();
            games = games.Where(g =>
                g.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.Author!.Username.ToLower() == usernameLower));
        }

        // Participating filter - current user is master, mentor, assistant, player, or reader
        if (query.Participating == true)
        {
            games = games.Where(g =>
                // Master
                g.MasterId == userId ||
                // Mentor
                g.MentorId == userId ||
                // Assistant
                g.Assistants.Any(a => a.UserId == userId) ||
                // Player (has active character)
                g.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.AuthorId == userId) ||
                // Reader (subscriber)
                _dbContext.Subscriptions.Any(s =>
                    s.TargetType == SubscriptionTargetType.Game &&
                    s.TargetId == g.GameId &&
                    s.SubscriberId == userId));
        }

        return games;
    }

    private IOrderedQueryable<DbGame> ApplySorting(IQueryable<DbGame> games, GamesQuery query)
    {
        var sortBy = query.SortBy?.ToLowerInvariant();
        var isAscending = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        // When searching without explicit sort, use relevance-based ordering
        if (!string.IsNullOrWhiteSpace(query.Search) && string.IsNullOrEmpty(query.SortBy))
        {
            var searchLower = query.Search.ToLower();
            return games
                .OrderByDescending(g => g.Title.ToLower() == searchLower) // Exact match first
                .ThenByDescending(g => EF.Functions.ILike(g.Title, query.Search + "%")) // Prefix match
                .ThenByDescending(g => EF.Functions.TrigramsSimilarity(g.Title, searchLower)) // Fuzzy match score
                .ThenBy(g => g.Title);
        }

        if (sortBy == "recruitmentstarted")
        {
            return isAscending
                ? games.OrderBy(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MaxValue).ThenBy(g => g.Title)
                : games.OrderByDescending(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MinValue).ThenBy(g => g.Title);
        }

        if (sortBy == "title")
        {
            return isAscending
                ? games.OrderBy(g => g.Title)
                : games.OrderByDescending(g => g.Title);
        }

        if (sortBy == "created")
        {
            // Sort by CreatedUtc (pure creation date)
            return isAscending
                ? games.OrderBy(g => g.CreatedUtc).ThenBy(g => g.Title)
                : games.OrderByDescending(g => g.CreatedUtc).ThenBy(g => g.Title);
        }

        if (sortBy == "activated")
        {
            // Sort by ActivatedUtc; games without it (Draft) go to end
            return isAscending
                ? games.OrderBy(g => g.ActivatedUtc.HasValue ? 0 : 1)
                    .ThenBy(g => g.ActivatedUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(g => g.Title)
                : games.OrderBy(g => g.ActivatedUtc.HasValue ? 0 : 1)
                    .ThenByDescending(g => g.ActivatedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(g => g.Title);
        }

        if (sortBy == "closed")
        {
            // Sort by ClosedUtc; games without it (Draft, Active) go to end
            return isAscending
                ? games.OrderBy(g => g.ClosedUtc.HasValue ? 0 : 1)
                    .ThenBy(g => g.ClosedUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(g => g.Title)
                : games.OrderBy(g => g.ClosedUtc.HasValue ? 0 : 1)
                    .ThenByDescending(g => g.ClosedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(g => g.Title);
        }

        if (sortBy == "popularity")
        {
            // Popularity uses pre-computed PopularityScore column (updated by PopularityScoreService)
            // Status grouping: Active first, then Closed, then Draft
            return isAscending
                ? games
                    .OrderBy(g => g.Status == ModuleStatus.Active)
                    .ThenBy(g => g.Status == ModuleStatus.Closed)
                    .ThenBy(g => g.PopularityScore)
                    .ThenByDescending(g => g.Title)
                : games
                    .OrderByDescending(g => g.Status == ModuleStatus.Active)
                    .ThenByDescending(g => g.Status == ModuleStatus.Closed)
                    .ThenByDescending(g => g.PopularityScore)
                    .ThenBy(g => g.Title);
        }

        if (sortBy == "status")
        {
            // Status order (asc): Draft(0), Active+first recruitment(1), Active+subsequent recruitment(2),
            //                     Active+not recruiting(3), Closed+Frozen(4), Closed+Finished(5), Closed+None(6)
            // Within each group: sort by relevant date (CreatedUtc/ActivatedUtc/ClosedUtc)
            return isAscending
                ? games
                    .OrderBy(g =>
                        g.Status == ModuleStatus.Draft ? 0 :
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount == 1 ? 1 :
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount >= 2 ? 2 :
                        g.Status == ModuleStatus.Active ? 3 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 4 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 5 : 6)
                    .ThenByDescending(g => g.Status == ModuleStatus.Draft ? g.CreatedUtc :
                                 g.Status == ModuleStatus.Active ? (g.ActivatedUtc ?? g.CreatedUtc) :
                                 (g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc))
                    .ThenBy(g => g.Title)
                : games
                    .OrderByDescending(g =>
                        g.Status == ModuleStatus.Draft ? 0 :
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount == 1 ? 1 :
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount >= 2 ? 2 :
                        g.Status == ModuleStatus.Active ? 3 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 4 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 5 : 6)
                    .ThenByDescending(g => g.Status == ModuleStatus.Draft ? g.CreatedUtc :
                                           g.Status == ModuleStatus.Active ? (g.ActivatedUtc ?? g.CreatedUtc) :
                                           (g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc))
                    .ThenBy(g => g.Title);
        }

        if (sortBy == "availableslots")
        {
            // Group order: Active+recruiting(0), Draft(1), Active+not recruiting(2),
            //              Closed+Frozen(3), Closed+Finished(4), Closed+None(5)
            // Within Active+recruiting: sort by RecruitmentStartedUtc
            return isAscending
                ? games
                    .OrderBy(g =>
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen ? 0 :
                        g.Status == ModuleStatus.Draft ? 1 :
                        g.Status == ModuleStatus.Active ? 2 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 3 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 4 : 5)
                    .ThenBy(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(g => g.Title)
                : games
                    .OrderBy(g =>
                        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen ? 0 :
                        g.Status == ModuleStatus.Draft ? 1 :
                        g.Status == ModuleStatus.Active ? 2 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 3 :
                        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 4 : 5)
                    .ThenByDescending(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(g => g.Title);
        }

        // Check if filtering only Closed games - sort by ClosedUtc
        var isClosedOnly = query.Statuses is { Count: 1 } && query.Statuses.Contains(ModuleStatus.Closed);
        if (isClosedOnly)
        {
            return isAscending
                ? games.OrderBy(g => g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc).ThenBy(g => g.Title)
                : games.OrderByDescending(g => g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc).ThenBy(g => g.Title);
        }

        // Default: activated (ActivatedUtc with fallback to CreatedUtc)
        return isAscending
            ? games.OrderBy(g => g.ActivatedUtc ?? g.CreatedUtc).ThenBy(g => g.Title)
            : games.OrderByDescending(g => g.ActivatedUtc ?? g.CreatedUtc).ThenBy(g => g.Title);
    }

    public async Task<IDictionary<Guid, IEnumerable<Guid>>> GetAvailableRoomIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdArray = gameIds.ToArray();

        var rooms = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => new { r.RoomId, r.GameId })
            .ToArrayAsync(ct);

        return rooms
            .GroupBy(g => g.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));
    }

    public async Task<IEnumerable<PostPendency>> GetPostPendencies(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdArray = gameIds.ToArray();

        var roomIds = await _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdArray.Contains(r.GameId))
            .Select(r => r.RoomId)
            .ToArrayAsync(ct);

        return await _dbContext.PostPendencies
            .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    public async Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PostPendency> postPendencies)> GetRoomsAndPostPendencies(
        IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();

        // Single query with Include instead of 2 separate queries
        var rooms = await _dbContext.Rooms
            .Include(r => r.PostPendencies.Where(p => !p.IsRemoved))
                .ThenInclude(p => p.WaitingForUser)
            .Include(r => r.PostPendencies.Where(p => !p.IsRemoved))
                .ThenInclude(p => p.CreatedBy)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .Where(r => gameIdList.Contains(r.GameId))
            .ToArrayAsync(ct);

        var roomsDict = rooms
            .GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));

        var postPendencies = rooms
            .SelectMany(r => r.PostPendencies)
            .Select(p => _mapper.Map<PostPendency>(p))
            .ToArray();

        return (roomsDict, postPendencies);
    }

    public async Task<IDictionary<Guid, int>> GetTotalPostCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        // Single GROUP BY query instead of N subqueries per room
        return await _dbContext.Posts
            .Where(p => !p.IsRemoved && !p.Room.IsRemoved && gameIdList.Contains(p.Room.GameId))
            .GroupBy(p => p.Room.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, ct);
    }

    public async Task<IDictionary<Guid, int>> GetTotalCommentCounts(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await _dbContext.Comments
            .Where(c => !c.IsRemoved && gameIdList.Contains(c.EntityId))
            .GroupBy(c => c.EntityId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToArrayAsync(ct);

        return counts.ToDictionary(x => x.GameId, x => x.Count);
    }

    public async Task<GameDetails?> GetGameDetails(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            // Single-element enrichment — reuses the same batched helper
            // as the list / by-ids paths so the details endpoint returns
            // ActiveCharacters, Players, SubscriberUsernames, review
            // counts, etc. Previously these were silently empty on the
            // details path, which is why game tooltips on featured
            // posts showed no characters.
            await EnrichGamesAsync(new[] { game }, ct);
        }
        return game;
    }

    public Task<GameDto?> GetGame(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)!;
    }

    public Task<GameDto?> GetGameByPublicId(string publicId, Guid userId, CancellationToken ct = default)
    {
        return _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.PublicId == publicId)
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)!;
    }

    public async Task<GameDetails?> GetGameDetailsByPublicId(string publicId, Guid userId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.PublicId == publicId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            await EnrichGamesAsync(new[] { game }, ct);
        }
        return game;
    }

    public async Task<IEnumerable<GameTag>> GetTags(CancellationToken ct = default)
    {
        return await _dbContext.Tags
            .OrderBy(t => t.TagGroup.SortOrder)
            .ThenBy(t => t.SortOrder)
            .Select(t => new GameTag
            {
                Id = t.TagId,
                ShortId = t.ShortId,
                Title = t.Title,
                Description = t.Description,
                GroupTitle = t.TagGroup.Title,
                GroupDescription = t.TagGroup.Description,
                GroupSortOrder = t.TagGroup.SortOrder,
                SortOrder = t.SortOrder,
                GamesCount = t.GameTags.Count(gt => gt.Game.Status == ModuleStatus.Active)
            })
            .ToArrayAsync(ct);
    }

    public async Task<IEnumerable<GameDto>> GetByIds(IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return Array.Empty<GameDto>();

        var games = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => gameIdList.Contains(g.GameId))
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, ct);
        return games;
    }

    #endregion

    #region Write Operations

    public async Task<GameDetails> Create(CreateGameEntity game, CreateRoomEntity room, CancellationToken ct = default)
    {
        var dbGame = new DbGame
        {
            GameId = game.GameId,
            CreatedUtc = game.CreatedUtc,
            Status = game.Status,
            DraftVisibility = game.DraftVisibility,
            ActivatedUtc = game.ActivatedUtc,
            MasterId = game.MasterId,
            Title = game.Title.Trim(),
            SystemName = game.SystemName?.Trim(),
            NarrativeSetting = game.NarrativeSetting?.Trim(),
            Info = game.Info?.Trim(),
            HideTemper = game.HideTemper,
            HideSkills = game.HideSkills,
            HideInventory = game.HideInventory,
            HideStory = game.HideStory,
            DisableAlignment = game.DisableAlignment,
            HideDiceResult = game.HideDiceResult,
            ShowPrivateMessages = game.ShowPrivateMessages,
            HidePostStats = game.HidePostStats,
            CommentsAccessMode = game.CommentsAccessMode,
            AttributeSchemaId = game.AttributeSchemaId,
            IsRemoved = false,
            IsRecruitmentOpen = game.IsRecruitmentOpen,
            RecruitmentStartedUtc = game.RecruitmentStartedUtc,
            RecruitmentCount = game.RecruitmentCount
        };

        var dbRoom = new DbRoom
        {
            RoomId = room.RoomId,
            GameId = room.GameId,
            Title = room.Title.Trim(),
            Type = room.Type,
            AccessType = room.AccessType,
            ViewPrivateText = room.ViewPrivateText,
            ViewDiceResults = room.ViewDiceResults,
            DiceEnabled = room.DiceEnabled,
            OrderNumber = room.OrderNumber,
            IsRemoved = false
        };

        var dbTags = game.TagIds.Select(tagId => new DbTag
        {
            GameTagId = _guidFactory.Create(),
            GameId = game.GameId,
            TagId = tagId
        });

        // Temporary unique placeholder for PublicId (will be updated after SerialNumber is generated)
        dbGame.PublicId = $"t{game.GameId:N}"[..10];

        await _dbContext.Games.AddAsync(dbGame, ct);
        await _dbContext.Rooms.AddAsync(dbRoom, ct);
        await _dbContext.GameTags.AddRangeAsync(dbTags, ct);
        await _dbContext.SaveChangesAsync(ct);

        // Generate PublicId from SerialNumber (which was auto-generated on insert)
        dbGame.PublicId = _publicIdService.Encode(dbGame.SerialNumber);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == game.GameId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task<GameDetails> Update(UpdateGameEntity updateGame, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([updateGame.GameId], ct);
        if (game == null)
            throw new InvalidOperationException($"Game {updateGame.GameId} not found");

        // Update fields if provided
        if (updateGame.Status.HasValue)
        {
            // Reset inactivity warnings when game is reactivated
            if (updateGame.Status.Value == ModuleStatus.Active && game.Status != ModuleStatus.Active)
            {
                game.InactivityWarningUtc = null;
                game.ClosureWarningUtc = null;
            }
            game.Status = updateGame.Status.Value;
        }

        if (updateGame.PremoderationStatus.HasValue)
            game.PremoderationStatus = updateGame.PremoderationStatus.Value;

        if (updateGame.ClosedReason.HasValue)
            game.ClosedReason = updateGame.ClosedReason.Value;

        if (updateGame.DraftVisibility.HasValue)
            game.DraftVisibility = updateGame.DraftVisibility.Value;

        if (updateGame.IsRecruitmentOpen.HasValue)
            game.IsRecruitmentOpen = updateGame.IsRecruitmentOpen.Value;

        if (updateGame.IncrementRecruitmentCount)
            game.RecruitmentCount++;

        if (updateGame.RecruitmentPcLimit.HasValue)
            game.RecruitmentPcLimit = updateGame.RecruitmentPcLimit;

        if (!string.IsNullOrEmpty(updateGame.Title))
            game.Title = updateGame.Title.Trim();

        if (!string.IsNullOrEmpty(updateGame.SystemName))
            game.SystemName = updateGame.SystemName.Trim();

        if (!string.IsNullOrEmpty(updateGame.NarrativeSetting))
            game.NarrativeSetting = updateGame.NarrativeSetting.Trim();

        if (!string.IsNullOrEmpty(updateGame.Info))
            game.Info = updateGame.Info.Trim();

        if (updateGame.HideTemper.HasValue)
            game.HideTemper = updateGame.HideTemper.Value;

        if (updateGame.HideSkills.HasValue)
            game.HideSkills = updateGame.HideSkills.Value;

        if (updateGame.HideInventory.HasValue)
            game.HideInventory = updateGame.HideInventory.Value;

        if (updateGame.HideStory.HasValue)
            game.HideStory = updateGame.HideStory.Value;

        if (updateGame.DisableAlignment.HasValue)
            game.DisableAlignment = updateGame.DisableAlignment.Value;

        if (updateGame.HideDiceResult.HasValue)
            game.HideDiceResult = updateGame.HideDiceResult.Value;

        if (updateGame.ShowPrivateMessages.HasValue)
            game.ShowPrivateMessages = updateGame.ShowPrivateMessages.Value;

        if (updateGame.HidePostStats.HasValue)
            game.HidePostStats = updateGame.HidePostStats.Value;

        if (updateGame.CommentsAccessMode.HasValue)
            game.CommentsAccessMode = updateGame.CommentsAccessMode.Value;

        if (updateGame.ActivatedUtc.HasValue)
            game.ActivatedUtc = updateGame.ActivatedUtc.Value;

        if (updateGame.ClosedUtc.HasValue)
            game.ClosedUtc = updateGame.ClosedUtc.Value;

        if (updateGame.ClearClosedUtc)
            game.ClosedUtc = null;

        // Update tags if provided
        if (updateGame.TagIds != null && updateGame.TagIds.Any())
        {
            var existingTags = await _dbContext.GameTags
                .Where(t => t.GameId == updateGame.GameId)
                .ToListAsync(ct);

            _dbContext.GameTags.RemoveRange(existingTags);

            var newTags = updateGame.TagIds.Select(tagId => new DbTag
            {
                GameTagId = _guidFactory.Create(),
                GameId = updateGame.GameId,
                TagId = tagId
            });

            _dbContext.GameTags.AddRange(newTags);
        }

        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == updateGame.GameId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task Delete(Guid gameId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([gameId], ct);
        if (game != null)
        {
            game.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion
}
