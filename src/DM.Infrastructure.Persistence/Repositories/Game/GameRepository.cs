using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Core.Identity;
using DbToken = DM.Infrastructure.Persistence.Entities.Account.Token;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
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
        // The GameId tiebreaker makes the ordering unique so split-query
        // pagination stays deterministic: each collection subquery re-runs the
        // same ORDER BY + OFFSET/FETCH and must select the identical page.
        // That fixes the ordering, not the snapshot: the subqueries are separate
        // statements under READ COMMITTED, so a game created or removed between
        // them shifts the OFFSET window and a card can render with another card's
        // tags. Accepted deliberately — the window is milliseconds against a
        // handful of writes a day, and the damage is one wrong collection on one
        // render.
        //
        // The cure is not keyset pagination and does not touch the API contract:
        // materialise the page of identifiers first and fetch the collections by
        // Where(id => ids.Contains(...)), the way EnrichGamesAsync already fetches
        // everything else, and the collection queries stop replaying the OFFSET.
        // The price is one more round trip on the busiest list on the site, paid on
        // every page by every visitor, against a defect that needs a concurrent
        // create to appear at all. Revisit if game creation ever becomes
        // high-volume — that is the number that moves this trade, not the contract.
        var orderedGames = ApplySorting(gamesQuery, query).ThenBy(g => g.GameId);

        var games = await orderedGames
            .Page(pagingData)
            // GameDto projects three independent collections (GameTags,
            // Assistants, BlackList); a single query LEFT-JOINs them into a
            // cartesian product that can exhaust memory (BufferedDataReader
            // OOM). AsSplitQuery loads each collection with its own query.
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, userId, ct);
        await EnrichWithFilteredPlayerCharactersAsync(games, query.PlayerUsername, ct);
        return games;
    }

    /// <summary>
    /// Populate <see cref="GameDto.FilteredPlayerCharacters"/> for the
    /// player-filtered list (profile games table). Only runs when the
    /// query carries a player filter - the field stays null otherwise,
    /// so unfiltered lists pay no extra query. Returns ALL of that
    /// player's non-NPC characters regardless of the query's
    /// PlayerParticipation scope: with the default Active scope the filter
    /// guarantees at least one active character per game (actives are
    /// ordered first), while with PlayerParticipation.Any a row may carry
    /// only retired characters or an application under review. Declined
    /// applications are excluded - a rejected application never was a
    /// character in the game.
    /// </summary>
    private async Task EnrichWithFilteredPlayerCharactersAsync(
        IReadOnlyList<GameDto> games,
        string? playerUsername,
        CancellationToken ct)
    {
        if (games.Count == 0 || string.IsNullOrEmpty(playerUsername)) return;

        var gameIds = games.Select(g => g.Id).ToHashSet();
        // Case-insensitive per USERNAME_POLICY.md, same as the filter itself.
        var usernameLower = playerUsername.ToLower();

        var characterData = await _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) &&
                        !c.IsRemoved &&
                        !c.IsNpc &&
                        c.Status != CharacterStatus.Declined &&
                        c.Author!.Username.ToLower() == usernameLower)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.GameId, c.Name, c.Status, c.IsDead, c.IsPlayerLeft, c.IsPlayerExiled })
            .ToListAsync(ct);

        var charactersMap = characterData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                // Active characters first, then retired/pending in creation order.
                g => g.OrderBy(c => c.Status == CharacterStatus.Active ? 0 : 1)
                    .Select(c => new PlayerCharacterInfo
                    {
                        Name = c.Name,
                        Status = c.Status,
                        IsDead = c.IsDead,
                        IsPlayerLeft = c.IsPlayerLeft,
                        IsPlayerExiled = c.IsPlayerExiled
                    })
                    .ToList());

        foreach (var game in games)
        {
            game.FilteredPlayerCharacters = charactersMap.GetValueOrDefault(game.Id, []);
        }
    }

    /// <summary>
    /// Populate computed / aggregated fields on Game DTOs that cannot
    /// be produced by the ProjectTo mapping (ActiveCharacters, Players,
    /// subscriber summary, Recruitment.PcCount, GameReviewsCount,
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
    /// <param name="games">Games to hydrate</param>
    /// <param name="userId">
    /// The user the read is on behalf of. Only <see cref="GameDto.IsViewerSubscriber" />
    /// depends on it; every other field here is the same for all viewers.
    /// <see cref="Guid.Empty" /> for an anonymous read.
    /// </param>
    /// <param name="ct">Cancellation token</param>
    private async Task EnrichGamesAsync<T>(
        IReadOnlyList<T> games,
        Guid userId,
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
        // Projected, not materialized: selecting the Author navigation pulled whole
        // User rows — Salt and PasswordHash included — into memory for every active
        // character on the page, and left Picture empty besides, because AvatarUpload
        // is not loaded and lazy loading is off.
        var playerData = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => gameIds.Contains(c.GameId) && c.Status == CharacterStatus.Active && !c.IsNpc &&
                        c.AuthorId.HasValue && c.Author != null)
            .Select(c => new
            {
                c.GameId,
                Author = new GeneralUser
                {
                    UserId = c.Author!.UserId,
                    Username = c.Author.Username,
                    Role = c.Author.Role,
                    Status = c.Author.Status,
                    LastActivityUtc = c.Author.LastActivityUtc,
                    RegisteredUtc = c.Author.CreatedUtc,
                    // Carried on purpose: IsNewbie derives from it, and dropping it
                    // would silently mark every player a newbie.
                    QuantityRating = c.Author.QuantityRating,
                    Picture = AvatarProjections.From(c.Author.AvatarUpload),
                },
            })
            .ToListAsync(ct);

        var playersMap = playerData
            .GroupBy(c => c.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.Author).DistinctBy(u => u.UserId).ToList());

        // Subscriber summary — the total, whether this viewer is one of them, and
        // the capped preview of names, all from one statement. The three fields
        // are everything the consumers ever asked the subscriber list for: two
        // counts and a Contains(viewerId). Loading the ids to answer them read
        // every subscription row of every game on the page.
        //
        // EF 8 / Npgsql 8 translate this to a single SELECT: a GROUP BY for the
        // aggregates, LEFT JOIN'ed to a ROW_NUMBER() OVER (PARTITION BY TargetId)
        // subquery for the preview (the window form rather than LATERAL because
        // the inner order is by a column of the joined Users row). The viewer
        // flag becomes an EXISTS over the same (TargetType, TargetId) index with
        // an equality on SubscriberId — no join, so unlike a conditional count
        // over the navigation it does not re-scan Users per group.
        var subscriberSummaries = await _dbContext.Subscriptions
            .Where(s => s.TargetType == SubscriptionTargetType.Game && gameIds.Contains(s.TargetId))
            .GroupBy(s => s.TargetId)
            .Select(g => new
            {
                GameId = g.Key,
                // Distinct subscribers, not subscription rows. The pair is unique in
                // the schema now, so the two forms agree; the distinct one stays
                // because it is what the column means, and because the count has to
                // keep meaning that if the rows ever arrive from an import rather
                // than from the subscribe path.
                Count = g.Select(s => s.SubscriberId).Distinct().Count(),
                ViewerSubscribed = g.Any(s => s.SubscriberId == userId),
                // Subscribers who have never been active must sort last, and a
                // plain DESC in Postgres puts nulls first.
                Preview = g.OrderByDescending(s => s.Subscriber.LastActivityUtc != null)
                    .ThenByDescending(s => s.Subscriber.LastActivityUtc)
                    .ThenBy(s => s.SubscriptionId)
                    .Take(SubscriptionPolicy.PreviewCap)
                    .Select(s => s.Subscriber.Username)
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.GameId, ct);

        foreach (var game in games)
        {
            game.Players = playersMap.TryGetValue(game.Id, out var players)
                ? players.ToList()
                : [];

            var summary = subscriberSummaries.GetValueOrDefault(game.Id);
            game.SubscribersCount = summary?.Count ?? 0;
            game.IsViewerSubscriber = summary?.ViewerSubscribed ?? false;
            game.SubscriberUsernames = summary?.Preview ?? [];
        }

        // Invitation tokens — single query instead of N subqueries. Projected for the
        // reason the players block above is: including the User navigation pulled whole
        // User rows, Salt and PasswordHash included, into memory for every invitation on
        // the page, and left PendingAssistant.Picture empty besides, because
        // AvatarUpload is not loaded and lazy loading is off.
        var tokens = await _dbContext.Tokens
            .AsNoTracking()
            .Where(t => !t.IsRemoved &&
                        t.EntityId.HasValue &&
                        gameIds.Contains(t.EntityId.Value) &&
                        (t.Type == TokenType.GameAssistantInvitation ||
                         t.Type == TokenType.GamePlayerInvitation ||
                         t.Type == TokenType.GameReaderInvitation))
            .Select(t => new
            {
                GameId = t.EntityId!.Value,
                t.Type,
                t.UserId,
                User = new GeneralUser
                {
                    UserId = t.User.UserId,
                    Username = t.User.Username,
                    Role = t.User.Role,
                    Status = t.User.Status,
                    LastActivityUtc = t.User.LastActivityUtc,
                    RegisteredUtc = t.User.CreatedUtc,
                    QuantityRating = t.User.QuantityRating,
                    Picture = AvatarProjections.From(t.User.AvatarUpload),
                },
            })
            .ToListAsync(ct);

        var tokensByGame = tokens.GroupBy(t => t.GameId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var game in games)
        {
            if (tokensByGame.TryGetValue(game.Id, out var gameTokens))
            {
                var assistantToken = gameTokens.FirstOrDefault(t => t.Type == TokenType.GameAssistantInvitation);
                game.PendingAssistant = assistantToken?.User;
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
            game.ActiveCharacters = activeCharactersMap.GetValueOrDefault(game.Id, []);
        }
    }

    /// <summary>
    /// Populate the info-table aggregates that only the game details page needs
    /// (total / master post counts, last master post, dice support). Kept off
    /// the list and by-ids paths so those pay no extra queries. Every query is a
    /// plain aggregate / EXISTS over a single game - no collection Include, so it
    /// stays split-query safe and cannot cartesian-explode.
    /// </summary>
    private async Task EnrichGameDetailsAsync(GameDetails game, CancellationToken ct)
    {
        var masterId = game.Master.UserId;

        // Posts across all live rooms of the game.
        var gamePosts = _dbContext.Posts
            .Where(p => !p.IsRemoved && !p.Room.IsRemoved && p.Room.GameId == game.Id);

        game.TotalPostsCount = await gamePosts.CountAsync(ct);
        game.MasterPostsCount = await gamePosts.CountAsync(p => p.AuthorId == masterId, ct);
        game.LastMasterPostUtc = await gamePosts
            .Where(p => p.AuthorId == masterId)
            .MaxAsync(p => (DateTimeOffset?)p.CreatedUtc, ct);

        // Dice support = any live room of the game has dice rolling enabled.
        game.DiceSupported = await _dbContext.Rooms
            .AnyAsync(r => !r.IsRemoved && r.GameId == game.Id && r.DiceEnabled, ct);
    }

    private IQueryable<DbGame> ApplyFilters(IQueryable<DbGame> games, GamesQuery query, Guid userId)
    {
        // Premoderation filter (service-gated, Mentor+ only): the moderation
        // worklist must show ALL games in the requested premoderation states,
        // while the regular accessibility scope hides exactly those games
        // from non-participants. Switch to the moderation scope instead.
        if (query.PremoderationStatuses is { Count: > 0 })
        {
            var premoderationStatuses = query.PremoderationStatuses.ToArray();
            games = games.Where(g => !g.IsRemoved && premoderationStatuses.Contains(g.PremoderationStatus));
        }
        else
        {
            games = games.Where(GameAccessibilityFilters.GameAvailable(userId));
        }

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
        if (query.CreatedFromUtc.HasValue)
        {
            games = games.Where(g => g.CreatedUtc >= query.CreatedFromUtc.Value);
        }
        if (query.CreatedToUtc.HasValue)
        {
            games = games.WhereAtOrBefore(g => g.CreatedUtc, query.CreatedToUtc.Value);
        }

        // ActivatedUtc - exclude games without this date if filter is set
        if (query.ActivatedFromUtc.HasValue)
        {
            games = games.Where(g => g.ActivatedUtc.HasValue && g.ActivatedUtc.Value >= query.ActivatedFromUtc.Value);
        }
        if (query.ActivatedToUtc.HasValue)
        {
            games = games.WhereAtOrBefore(g => g.ActivatedUtc, query.ActivatedToUtc.Value);
        }

        // ClosedUtc - exclude games without this date if filter is set
        if (query.ClosedFromUtc.HasValue)
        {
            games = games.Where(g => g.ClosedUtc.HasValue && g.ClosedUtc.Value >= query.ClosedFromUtc.Value);
        }
        if (query.ClosedToUtc.HasValue)
        {
            games = games.WhereAtOrBefore(g => g.ClosedUtc, query.ClosedToUtc.Value);
        }

        // RecruitmentStartedUtc - exclude games without this date if filter is set
        if (query.RecruitmentStartedFromUtc.HasValue)
        {
            games = games.Where(g => g.RecruitmentStartedUtc.HasValue &&
                                     g.RecruitmentStartedUtc.Value >= query.RecruitmentStartedFromUtc.Value);
        }
        if (query.RecruitmentStartedToUtc.HasValue)
        {
            games = games.WhereAtOrBefore(g => g.RecruitmentStartedUtc, query.RecruitmentStartedToUtc.Value);
        }

        // Player filter (case-insensitive per USERNAME_POLICY.md).
        // Scope: Active (default) - user has an active character, the public
        // /games semantics ("player = participant with an active character");
        // Any - any non-NPC character except declined applications, i.e.
        // retired characters and applications under review also match
        // (profile dossier semantics).
        if (!string.IsNullOrEmpty(query.PlayerUsername))
        {
            var usernameLower = query.PlayerUsername.ToLower();
            games = query.PlayerParticipation == PlayerParticipation.Any
                ? games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && !c.IsNpc && c.Status != CharacterStatus.Declined &&
                    c.Author!.Username.ToLower() == usernameLower))
                : games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && c.Status == CharacterStatus.Active &&
                    c.Author!.Username.ToLower() == usernameLower));
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
            .Where(p => roomIds.Contains(p.RoomId))
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
    }

    public async Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PostPendency> postPendencies)> GetRoomsAndPostPendencies(
        IEnumerable<Guid> gameIds, Guid userId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();

        // Single query with Include instead of 2 separate queries
        var rooms = await _dbContext.Rooms
            .Include(r => r.PostPendencies)
                .ThenInclude(p => p.WaitingForUser)
            .Include(r => r.PostPendencies)
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
        // GameDetails projects several independent collections (Tags,
        // Assistants, FullAssistants, Characters, BlackList). A single query
        // LEFT-JOINs them into a cartesian product that can exhaust memory
        // (BufferedDataReader OOM); AsSplitQuery loads each collection with
        // its own query instead.
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            // Single-element enrichment — reuses the same batched helper
            // as the list / by-ids paths so the details endpoint returns
            // ActiveCharacters, Players, SubscriberUsernames, review
            // counts, etc. Previously these were silently empty on the
            // details path, which is why game tooltips on featured
            // posts showed no characters.
            await EnrichGamesAsync(new[] { game }, userId, ct);
            await EnrichGameDetailsAsync(game, ct);
        }
        return game;
    }

    public async Task<GameDto?> GetGame(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.GameId == gameId)
            // GameDto's three collections (GameTags/Assistants/BlackList) would
            // otherwise multiply into a cartesian product on a single query.
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            // Players and the subscriber summary are ignored by the projection and
            // filled only here. This load is the one every authorization decision
            // about a game is made on — GetRoles cannot see a player at all without
            // it, so without this call an accepted player is indistinguishable from
            // a stranger: private-comment access and the ban's own-game exemption
            // both resolve against an empty player list. The same argument now
            // covers the Reader role, which reads IsViewerSubscriber: the userId
            // this method already filters visibility by is the one it is filled for.
            await EnrichGamesAsync(new[] { game }, userId, ct);
        }

        return game;
    }

    public Task<Guid?> FindGameIdByPublicId(string publicId, Guid userId, CancellationToken ct = default)
    {
        // Same visibility filter as the aggregate read, so the set of public ids
        // that resolve is identical — but one scalar column instead of a game,
        // its players, its readers and its unread counters, all of which the
        // callers of this throw away.
        return _dbContext.Games
            .TagWith("DM.Game.FindIdByPublicId")
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.PublicId == publicId)
            .Select(g => (Guid?)g.GameId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<GameDto?> GetGameByPublicId(string publicId, Guid userId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.PublicId == publicId)
            // See GetGame: AsSplitQuery avoids the multi-collection cartesian.
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        // The projection cannot produce these, so without this the same endpoint
        // answered two different payloads: addressed by its five-letter alias a
        // game reported no subscribers, no active characters and pcCount 0, and
        // addressed by its GUID it reported all three. It also decided
        // authorization on a different set of facts — PendingInvitedUserIds is
        // filled here and nowhere else, so GameIntention.Read refused a user
        // holding a pending invitation to a hidden game by alias and served it
        // by GUID.
        if (game != null)
        {
            await EnrichGamesAsync(new[] { game }, userId, ct);
        }

        return game;
    }

    public async Task<GameDetails?> GetGameDetailsByPublicId(string publicId, Guid userId, CancellationToken ct = default)
    {
        // See GetGameDetails: AsSplitQuery avoids the multi-collection
        // cartesian product that OOMs the single-query reader.
        var game = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => g.PublicId == publicId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (game is not null)
        {
            await EnrichGamesAsync(new[] { game }, userId, ct);
            await EnrichGameDetailsAsync(game, ct);
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

    public async Task<IEnumerable<GameDto>> GetByIds(
        IEnumerable<Guid> gameIds, Guid userId, Guid viewerId, CancellationToken ct = default)
    {
        var gameIdList = gameIds.ToList();
        if (gameIdList.Count == 0)
            return Array.Empty<GameDto>();

        var games = await _dbContext.Games
            .Where(GameAccessibilityFilters.GameAvailable(userId))
            .Where(g => gameIdList.Contains(g.GameId))
            // GameDto's GameTags/Assistants/BlackList collections cartesian-
            // explode on a single query; split them (EF orders each split by
            // the parent key automatically when there is no row limiting).
            .ProjectTo<GameDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, viewerId, ct);
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
            // The game itself is inserted by this call, so nobody else can hold
            // a room in it yet and its first room is number 1 by construction.
            // Every later room takes MAX+1 under the game row lock, in
            // RoomRepository.Create.
            RoomNumber = 1,
            Title = room.Title.Trim(),
            Type = room.Type,
            AccessType = room.AccessType,
            ViewPrivateText = room.ViewPrivateText,
            ViewDiceResults = room.ViewDiceResults,
            DiceEnabled = room.DiceEnabled,
            OrderNumber = room.OrderNumber,
            IsRemoved = false
        };

        // A set, not a list: the same tag twice is the same tag, and the pair is
        // unique in the schema.
        var dbTags = game.TagIds.Distinct().Select(tagId => new DbTag
        {
            GameTagId = _guidFactory.Create(),
            GameId = game.GameId,
            TagId = tagId
        });

        // The readable address is taken before the insert instead of being stamped by
        // a second SaveChanges — see SerialNumberAllocator for what that pair cost.
        dbGame.SerialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext, ct);
        dbGame.PublicId = _publicIdService.Encode(dbGame.SerialNumber);

        await _dbContext.Games.AddAsync(dbGame, ct);
        await _dbContext.Rooms.AddAsync(dbRoom, ct);
        await _dbContext.GameTags.AddRangeAsync(dbTags, ct);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Games
            .Where(g => g.GameId == game.GameId)
            .ProjectTo<GameDetails>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
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

        if (updateGame.SetMentorId)
            game.MentorId = updateGame.MentorId;

        if (updateGame.ClearRecruitmentStartedUtc)
            game.RecruitmentStartedUtc = null;

        // Update tags if provided
        if (updateGame.TagIds != null && updateGame.TagIds.Any())
        {
            // The difference, not a wholesale replacement: deleting a row and
            // inserting another one for the same pair in a single SaveChanges puts two
            // statements against one unique key into one batch, and nothing here needs
            // that. Rows that stay are left alone, which also keeps their identifiers.
            var requestedTagIds = updateGame.TagIds.Distinct().ToList();

            var existingTags = await _dbContext.GameTags
                .Where(t => t.GameId == updateGame.GameId)
                .ToListAsync(ct);

            _dbContext.GameTags.RemoveRange(
                existingTags.Where(t => !requestedTagIds.Contains(t.TagId)));

            var existingTagIds = existingTags.Select(t => t.TagId).ToHashSet();
            var newTags = requestedTagIds
                .Where(tagId => !existingTagIds.Contains(tagId))
                .Select(tagId => new DbTag
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
            .AsSplitQuery()
            .FirstAsync(ct);
    }

    public async Task Delete(Guid gameId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var game = await _dbContext.Games.FindAsync([gameId], ct);
        if (game != null)
        {
            SoftDelete.Mark(game, deletedByUserId, _dateTimeProvider.Now);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    #endregion
}
