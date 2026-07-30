using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Caching;

namespace DM.Domain.Community.Features.Statistics;

/// <inheritdoc />
internal class CommunityStatsService : ICommunityStatsService
{
    private readonly ICommunityStatsRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICache _cache;

    // Longer TTL than the frontend poll interval (60s) so the vast majority of
    // SiteStatistics requests hit a warm cache. The stats are approximate by
    // design — "Users: 12,345, online: 42" is a vibe number, not a real-time
    // metric — so a two-minute staleness window is well within acceptable
    // drift. The heavy 15-query path only runs on genuine cache misses.
    private static readonly TimeSpan LiveStatsCacheDuration = TimeSpan.FromMinutes(2);

    // Closed calendar periods are immutable (new content is always dated
    // "now"), so their leaderboards can live in cache for a long time; only
    // soft-deletes of old content can retroactively change them, hence a day
    // rather than forever. The current (still-open) period keeps changing as
    // reviews and posts arrive, so it gets a short TTL.
    private static readonly TimeSpan ClosedPeriodCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan OpenPeriodCacheDuration = TimeSpan.FromMinutes(10);

    public CommunityStatsService(
        ICommunityStatsRepository repository,
        IDateTimeProvider dateTimeProvider,
        ICache cache)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    /// <inheritdoc />
    public Task<LiveStats> GetLiveStatsAsync(CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(
            "LiveStats",
            () => _repository.GetLiveStats(ct),
            LiveStatsCacheDuration);

    /// <inheritdoc />
    public Task<Leaderboards> GetLeaderboardsAsync(int year, int? month, CancellationToken ct = default)
    {
        var (startDate, endDate) = ResolvePeriodBounds(year, month);
        var cacheDuration = endDate <= _dateTimeProvider.Now
            ? ClosedPeriodCacheDuration
            : OpenPeriodCacheDuration;

        return _cache.GetOrCreateAsync(
            $"Leaderboards_{year}_{month}",
            () => CalculateLeaderboards(year, month, startDate, endDate, ct),
            cacheDuration);
    }

    private async Task<Leaderboards> CalculateLeaderboards(
        int year, int? month, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
    {
        // Contract: every board carries POSITIVE scores only (the "only
        // positive achievement is celebrated" product rule), enforced in the
        // query so no consumer has to re-filter. The SQL tie-break on the
        // entity id makes the board boundary deterministic across runs; the
        // display ranks (with ties broken by name) are assigned here.
        var boards = await _repository.GetLeaderboards(startDate, endDate, ct);

        AssignCompetitionRanks(boards.TopPlayersByRating);
        AssignCompetitionRanks(boards.TopPlayersByPosts);
        AssignCompetitionRanks(boards.TopGamesByRating);
        AssignCompetitionRanks(boards.TopGamesByPosts);
        AssignCompetitionRanks(boards.TopPlayersByVolume);
        AssignCompetitionRanks(boards.TopBlogsByRating);
        AssignCompetitionRanks(boards.TopBlogsByPosts);
        AssignCompetitionRanks(boards.TopBlogAuthorsByVolume);

        return new Leaderboards
        {
            Period = new Period { Year = year, Month = month },
            TopPlayersByRating = boards.TopPlayersByRating.ToArray(),
            TopPlayersByPosts = boards.TopPlayersByPosts.ToArray(),
            TopGamesByRating = boards.TopGamesByRating.ToArray(),
            TopGamesByPosts = boards.TopGamesByPosts.ToArray(),
            TopPlayersByVolume = boards.TopPlayersByVolume.ToArray(),
            TopBlogsByRating = boards.TopBlogsByRating.ToArray(),
            TopBlogsByPosts = boards.TopBlogsByPosts.ToArray(),
            TopBlogAuthorsByVolume = boards.TopBlogAuthorsByVolume.ToArray()
        };
    }

    /// <summary>
    /// Resolves the [start, end) UTC bounds for a stats period. A year of 0 is
    /// the "all-time" period: the widest possible window so no row is filtered
    /// out by date. Otherwise a calendar year or a specific calendar month.
    /// Internal for unit testing of the period-window logic.
    /// </summary>
    internal static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriodBounds(int year, int? month)
    {
        if (year <= 0)
        {
            return (DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        }

        if (month.HasValue)
        {
            var monthStart = new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero);
            return (monthStart, monthStart.AddMonths(1));
        }

        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return (yearStart, yearStart.AddYears(1));
    }

    /// <summary>
    /// Sorts entries for display (score DESC, then name for a deterministic
    /// order among ties) and assigns plain ordinal ranks 1..N — a top list
    /// reads as a simple numbered list; ties are broken by name rather than
    /// sharing a rank number. Internal for unit testing.
    /// </summary>
    internal static void AssignCompetitionRanks(List<LeaderboardEntry> entries)
    {
        entries.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0
                ? byScore
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Rank = i + 1;
        }
    }
}
