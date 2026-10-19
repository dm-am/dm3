using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using GameDto = DM.Domain.Game.Features.Games.Game;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// The catalog of games: the filtered, sorted and paged list, and how it is asked
/// </summary>
internal partial class GameRepository
{
    public async Task<int> Count(GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        var asked = await AskedAccountsOf(query, ct);
        return await ApplyFilters(_dbContext.Games.AsQueryable(), query, userId, asked)
            .CountAsync(ct);
    }

    public async Task<IEnumerable<GameDto>> GetGames(PagingData pagingData, GamesQuery query, Guid userId, CancellationToken ct = default)
    {
        var asked = await AskedAccountsOf(query, ct);
        var gamesQuery = ApplyFilters(_dbContext.Games.AsQueryable(), query, userId, asked);
        // The GameId tiebreaker makes the ordering unique, so each split-query
        // collection subquery re-runs the same ORDER BY + OFFSET and selects the same page.
        // A game created or removed between the split statements can shift the page;
        // accepted, the cure is fetching the page of ids first.
        var orderedGames = ApplySorting(gamesQuery, query).ThenBy(g => g.GameId);

        var games = await orderedGames
            .Page(pagingData)
            .ProjectToGame()
            .AsSplitQuery()
            .ToArrayAsync(ct);

        await EnrichGamesAsync(games, userId, ct);
        await EnrichWithFilteredPlayerCharactersAsync(games, query.PlayerUsername, asked.Players, ct);
        return games;
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
                GroupMaxTagsPerGame = t.TagGroup.MaxTagsPerGame,
                SortOrder = t.SortOrder,
                GamesCount = t.GameTags.Count(gt => gt.Game.Status == ModuleStatus.Active)
            })
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// The accounts the host and player filters mean when they name "Аккаунт удален":
    /// the removed accounts, with the live hosts named alongside. Null where the name
    /// is not asked for - that filter keeps reading the name.
    /// </summary>
    private sealed record AskedAccounts(IReadOnlyCollection<Guid>? Hosts, IReadOnlyCollection<Guid>? Players);

    private async Task<AskedAccounts> AskedAccountsOf(GamesQuery query, CancellationToken ct) => new(
        await RemovedAccountScope.IdsIfAsked(_dbContext.Users, query.OwnerUsernames, ct),
        string.IsNullOrEmpty(query.PlayerUsername)
            ? null
            : await RemovedAccountScope.IdsIfAsked(_dbContext.Users, [query.PlayerUsername], ct));

    /// <summary>
    /// Populate <see cref="GameDto.FilteredPlayerCharacters"/> for the player-filtered
    /// list (profile games table); null without a player filter. Every non-NPC character
    /// of the player whatever the participation scope, actives first; a declined
    /// application never was a character.
    /// </summary>
    private async Task EnrichWithFilteredPlayerCharactersAsync(
        IReadOnlyList<GameDto> games,
        string? playerUsername,
        IReadOnlyCollection<Guid>? playerIds,
        CancellationToken ct)
    {
        if (games.Count == 0 || string.IsNullOrEmpty(playerUsername)) return;

        var gameIds = games.Select(g => g.Id).ToHashSet();
        // Case-insensitive per USERNAME_POLICY.md, same as the filter itself.
        var usernameLower = playerUsername.ToLower();

        // "Аккаунт удален" plays through every removed account's characters, read
        // by the author column (RemovedAccountScope); any other name, by the name.
        var characters = _dbContext.Characters
            .Where(c => gameIds.Contains(c.GameId) &&
                        !c.IsRemoved &&
                        !c.IsNpc &&
                        c.Status != CharacterStatus.Declined);
        characters = playerIds is null
            ? characters.Where(c => c.Author!.Username.ToLower() == usernameLower)
            : characters.Where(c => c.AuthorId.HasValue && playerIds.Contains(c.AuthorId.Value));

        var characterData = await characters
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

    private IQueryable<DbGame> ApplyFilters(
        IQueryable<DbGame> games, GamesQuery query, Guid userId, AskedAccounts asked)
    {
        games = WhereVisible(games, query, userId);
        games = WhereSearched(games, query);
        games = WhereStatuses(games, query);
        games = WhereTags(games, query);
        games = WhereHosts(games, query, asked);
        games = WhereDates(games, query);
        games = WherePlayers(games, query, asked);
        games = WhereParticipating(games, query, userId);
        games = WhereNotHidden(games, query);

        return games;
    }

    private static IQueryable<DbGame> WhereVisible(IQueryable<DbGame> games, GamesQuery query, Guid userId)
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

        return games;
    }

    private static IQueryable<DbGame> WhereSearched(IQueryable<DbGame> games, GamesQuery query)
    {
        // Text search with fuzzy matching (OR between title/system/setting)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchPattern = LikePatterns.Contains(query.Search);
            var searchLower = query.Search.ToLower();
            games = games.Where(g =>
                // Contains match (case-insensitive)
                EF.Functions.ILike(g.Title, searchPattern) ||
                (g.SystemName != null && EF.Functions.ILike(g.SystemName, searchPattern)) ||
                (g.NarrativeSetting != null && EF.Functions.ILike(g.NarrativeSetting, searchPattern)) ||
                // Fuzzy match with trigrams (typo tolerance)
                EF.Functions.TrigramsSimilarity(g.Title, searchLower) > LikePatterns.TrigramThreshold ||
                (g.SystemName != null && EF.Functions.TrigramsSimilarity(g.SystemName, searchLower) > LikePatterns.TrigramThreshold) ||
                (g.NarrativeSetting != null && EF.Functions.TrigramsSimilarity(g.NarrativeSetting, searchLower) > LikePatterns.TrigramThreshold));
        }

        return games;
    }

    private static IQueryable<DbGame> WhereStatuses(IQueryable<DbGame> games, GamesQuery query)
    {
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

        return games;
    }

    private static IQueryable<DbGame> WhereTags(IQueryable<DbGame> games, GamesQuery query)
    {
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

        return games;
    }

    private static IQueryable<DbGame> WhereHosts(IQueryable<DbGame> games, GamesQuery query, AskedAccounts asked)
    {
        // Owner filter - master OR assistant (case-insensitive per USERNAME_POLICY.md, OR logic).
        // Asked for "Аккаунт удален", by the identifiers of the removed accounts and of
        // the live hosts named with it, joining no account.
        var hostIds = asked.Hosts;
        if (hostIds is not null)
        {
            games = games.Where(g =>
                hostIds.Contains(g.MasterId) || g.Assistants.Any(a => hostIds.Contains(a.UserId)));
        }
        else if (query.OwnerUsernames is { Count: > 0 })
        {
            var usernamesLower = query.OwnerUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => u.ToLower())
                .ToArray();
            if (usernamesLower.Length > 0)
            {
                // The list lifts the account filter to keep the games of removed
                // masters (RemovedAuthors), so a removed login is refused in the
                // open: the old name must not find what the removal took off it.
                games = games.Where(g =>
                    (!g.Master.IsRemoved && usernamesLower.Contains(g.Master.Username.ToLower())) ||
                    g.Assistants.Any(a => !a.User.IsRemoved && usernamesLower.Contains(a.User.Username.ToLower())));
            }
        }

        return games;
    }

    private static IQueryable<DbGame> WhereDates(IQueryable<DbGame> games, GamesQuery query)
    {
        return games
            .WhereWithin(g => g.CreatedUtc, query.CreatedFromUtc, query.CreatedToUtc)
            .WhereWithin(g => g.ActivatedUtc, query.ActivatedFromUtc, query.ActivatedToUtc)
            .WhereWithin(g => g.ClosedUtc, query.ClosedFromUtc, query.ClosedToUtc)
            .WhereWithin(g => g.RecruitmentStartedUtc, query.RecruitmentStartedFromUtc, query.RecruitmentStartedToUtc);
    }

    private static IQueryable<DbGame> WherePlayers(IQueryable<DbGame> games, GamesQuery query, AskedAccounts asked)
    {
        // Player filter (case-insensitive per USERNAME_POLICY.md).
        // Scope: Active (default) - user has an active character, the public
        // /games semantics ("player = participant with an active character");
        // Any - any non-NPC character except declined applications, i.e.
        // retired characters and applications under review also match
        // (profile dossier semantics).
        var playerIds = asked.Players;
        if (playerIds is not null)
        {
            games = query.PlayerParticipation == PlayerParticipation.Any
                ? games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && !c.IsNpc && c.Status != CharacterStatus.Declined &&
                    c.AuthorId.HasValue && playerIds.Contains(c.AuthorId.Value)))
                : games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && c.Status == CharacterStatus.Active &&
                    c.AuthorId.HasValue && playerIds.Contains(c.AuthorId.Value)));
        }
        else if (!string.IsNullOrEmpty(query.PlayerUsername))
        {
            // A removed login matches nothing, for the reason the owner filter
            // above spells out.
            var usernameLower = query.PlayerUsername.ToLower();
            games = query.PlayerParticipation == PlayerParticipation.Any
                ? games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && !c.IsNpc && c.Status != CharacterStatus.Declined &&
                    !c.Author!.IsRemoved && c.Author.Username.ToLower() == usernameLower))
                : games.Where(g => g.Characters.Any(c =>
                    !c.IsRemoved && c.Status == CharacterStatus.Active &&
                    !c.Author!.IsRemoved && c.Author.Username.ToLower() == usernameLower));
        }

        return games;
    }

    private IQueryable<DbGame> WhereParticipating(IQueryable<DbGame> games, GamesQuery query, Guid userId)
    {
        // Participating filter - current user is master, mentor, assistant, player, or reader
        if (query.Participating == true)
        {
            games = games.Where(GameParticipationFilters.Participating(_dbContext, userId));
        }

        return games;
    }

    private static IQueryable<DbGame> WhereNotHidden(IQueryable<DbGame> games, GamesQuery query)
    {
        // The viewer's "Скрывать игры": the service decides which lists it cleans.
        if (query.ExcludedMasterIds is { Count: > 0 })
        {
            var excludedMasters = query.ExcludedMasterIds.ToArray();
            games = games.Where(g => !excludedMasters.Contains(g.MasterId));
        }

        return games;
    }

    /// <summary>
    /// Draft, Active in its first recruitment, Active in a later one, Active not
    /// recruiting, then Closed as Frozen, Finished and otherwise.
    /// </summary>
    private static readonly Expression<Func<DbGame, int>> StatusRank = g =>
        g.Status == ModuleStatus.Draft ? 0 :
        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount == 1 ? 1 :
        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen && g.RecruitmentCount >= 2 ? 2 :
        g.Status == ModuleStatus.Active ? 3 :
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 4 :
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 5 : 6;

    /// <summary>
    /// Recruiting first, then Draft, Active not recruiting, and Closed as Frozen,
    /// Finished and otherwise.
    /// </summary>
    private static readonly Expression<Func<DbGame, int>> SlotsRank = g =>
        g.Status == ModuleStatus.Active && g.IsRecruitmentOpen ? 0 :
        g.Status == ModuleStatus.Draft ? 1 :
        g.Status == ModuleStatus.Active ? 2 :
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Frozen ? 3 :
        g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished ? 4 : 5;

    private static IOrderedQueryable<DbGame> ApplySorting(IQueryable<DbGame> games, GamesQuery query)
    {
        var sortBy = query.SortBy?.ToLowerInvariant();
        var isAscending = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        // When searching without explicit sort, use relevance-based ordering
        if (!string.IsNullOrWhiteSpace(query.Search) && string.IsNullOrEmpty(query.SortBy))
        {
            var searchLower = query.Search.ToLower();
            var prefixPattern = LikePatterns.StartsWith(query.Search);
            return games
                .OrderByDescending(g => g.Title.ToLower() == searchLower) // Exact match first
                .ThenByDescending(g => EF.Functions.ILike(g.Title, prefixPattern)) // Prefix match
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
            return games.OrderByDirection(g => g.Title, isAscending);
        }

        if (sortBy == "created")
        {
            return games.OrderByDirection(g => g.CreatedUtc, isAscending).ThenBy(g => g.Title);
        }

        // Games without the moment go to the end both ways.
        if (sortBy == "activated")
        {
            return games.OrderBy(g => g.ActivatedUtc.HasValue ? 0 : 1)
                .ThenByDirection(g => g.ActivatedUtc, isAscending)
                .ThenBy(g => g.Title);
        }

        if (sortBy == "closed")
        {
            return games.OrderBy(g => g.ClosedUtc.HasValue ? 0 : 1)
                .ThenByDirection(g => g.ClosedUtc, isAscending)
                .ThenBy(g => g.Title);
        }

        if (sortBy == "popularity")
        {
            // Active first, then Closed, then Draft
            return games
                .OrderByDirection(g => g.Status == ModuleStatus.Active, isAscending)
                .ThenByDirection(g => g.Status == ModuleStatus.Closed, isAscending)
                .ThenByDirection(g => g.PopularityScore, isAscending)
                .ThenByDirection(g => g.Title, !isAscending);
        }

        if (sortBy == "status")
        {
            // Within each status group the newest moment of that status comes first.
            return games.OrderByDirection(StatusRank, isAscending)
                .ThenByDescending(g => g.Status == ModuleStatus.Draft ? g.CreatedUtc :
                                       g.Status == ModuleStatus.Active ? (g.ActivatedUtc ?? g.CreatedUtc) :
                                       (g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc))
                .ThenBy(g => g.Title);
        }

        if (sortBy == "availableslots")
        {
            // The groups keep their order both ways; the direction is that of the recruitment start.
            var grouped = games.OrderBy(SlotsRank);
            return (isAscending
                    ? grouped.ThenBy(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MaxValue)
                    : grouped.ThenByDescending(g => g.RecruitmentStartedUtc ?? DateTimeOffset.MinValue))
                .ThenBy(g => g.Title);
        }

        // Check if filtering only Closed games - sort by ClosedUtc
        var isClosedOnly = query.Statuses is { Count: 1 } && query.Statuses.Contains(ModuleStatus.Closed);
        if (isClosedOnly)
        {
            return games.OrderByDirection(g => g.ClosedUtc ?? g.ActivatedUtc ?? g.CreatedUtc, isAscending)
                .ThenBy(g => g.Title);
        }

        return games.OrderByDirection(g => g.ActivatedUtc ?? g.CreatedUtc, isAscending).ThenBy(g => g.Title);
    }
}
