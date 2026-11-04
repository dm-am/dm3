using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Community.Features.Statistics;

/// <inheritdoc />
internal class CommunityStatsService : ICommunityStatsService
{
    private readonly ICommunityStatsRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICache _cache;

    // Longer than the client's poll interval, so most requests hit a warm cache:
    // the numbers are approximate by design.
    private static readonly TimeSpan LiveStatsCacheDuration = TimeSpan.FromMinutes(2);

    // A closed period changes only by soft-deletes of old content, hence a day
    // rather than forever; the open one keeps changing.
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
        // A 400 rather than a DateTimeOffset construction throwing into a 500.
        var refusals = LeaderboardPeriod.Refusals(year, month);
        if (refusals.Count > 0)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>(refusals),
                RefusalMessage.PeriodInvalid);
        }

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
        // Positive scores only, enforced in the query; the display ranks are assigned here.
        var leaderboards = await _repository.GetLeaderboards(startDate, endDate, ct);

        foreach (var board in BoardsOf(leaderboards))
        {
            AssignOrdinalRanks(board);
        }

        leaderboards.Period = new Period { Year = year, Month = month };
        return leaderboards;
    }

    private static IEnumerable<List<LeaderboardEntry>> BoardsOf(Leaderboards leaderboards) =>
    [
        leaderboards.TopPlayersByRating,
        leaderboards.TopPlayersByPosts,
        leaderboards.TopGamesByRating,
        leaderboards.TopGamesByPosts,
        leaderboards.TopPlayersByVolume,
        leaderboards.TopBlogsByRating,
        leaderboards.TopBlogsByPosts,
        leaderboards.TopBlogAuthorsByVolume
    ];

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
    internal static void AssignOrdinalRanks(List<LeaderboardEntry> entries)
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
