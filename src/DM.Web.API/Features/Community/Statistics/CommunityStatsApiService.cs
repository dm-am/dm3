using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Web.API.Shared.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Web.API.Features.Community.Statistics;

/// <inheritdoc />
internal class CommunityStatsApiService : ICommunityStatsApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    // Closed calendar periods are immutable (new content is always dated
    // "now"), so their leaderboards can live in cache for a long time; only
    // soft-deletes of old content can retroactively change them, hence a day
    // rather than forever. The current (still-open) period keeps changing as
    // reviews and posts arrive, so it gets a short TTL.
    private static readonly TimeSpan ClosedPeriodCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan OpenPeriodCacheDuration = TimeSpan.FromMinutes(10);


    /// <inheritdoc />
    public CommunityStatsApiService(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Envelope<LiveStats>> GetLiveStats()
    {
        // Longer TTL than the frontend poll interval (60s) so the vast
        // majority of SiteStatistics requests hit a warm cache. The
        // stats are approximate by design — "Users: 12,345, online: 42"
        // is a vibe number, not a real-time metric — so a two-minute
        // staleness window is well within acceptable drift. The heavy
        // 15-query CalculateLiveStats path only runs on genuine cache
        // misses (~once every two minutes per instance).
        var liveStats = await _cache.GetOrCreateAsync("LiveStats", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await CalculateLiveStats();
        });

        return new Envelope<LiveStats>(liveStats!);
    }

    private async Task<LiveStats> CalculateLiveStats()
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = now.Date;
        var todayStartUtc = new DateTimeOffset(todayStart, TimeSpan.Zero);
        var onlineThreshold = now - OnlineThreshold;
        var weekAgo = now.AddDays(-7);

        // Online users count
        var onlineCount = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  &&
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold);

        // Totals with today's delta
        var totalUsers = await _dbContext.Users.CountAsync(u => !u.IsRemoved );
        var usersToday = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc >= todayStartUtc);

        var totalCharacters = await _dbContext.Characters.CountAsync(c => !c.IsRemoved);
        var charactersToday = await _dbContext.Characters.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc >= todayStartUtc);

        var totalGames = await _dbContext.Games.CountAsync(g => !g.IsRemoved);
        var gamesToday = await _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc >= todayStartUtc);

        var totalPosts = await _dbContext.Posts.CountAsync(p => !p.IsRemoved);
        var postsToday = await _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc >= todayStartUtc);

        // Count all blogs (including drafts and private)
        var totalBlogs = await _dbContext.Blogs.CountAsync(b => !b.IsRemoved);
        var blogsToday = await _dbContext.Blogs.CountAsync(b =>
            !b.IsRemoved && b.CreatedUtc >= todayStartUtc);

        // Count all publications (including unpublished drafts)
        var totalPublications = await _dbContext.Publications.CountAsync(p => !p.IsRemoved);
        var publicationsToday = await _dbContext.Publications.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc >= todayStartUtc);

        // Weekly best post (highest total rating from post reviews)
        var weeklyBest = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= weekAgo)
            .GroupBy(r => new { r.PostId, r.GameId })
            .Select(g => new
            {
                PostId = g.Key.PostId,
                GameId = g.Key.GameId,
                RatingSum = g.Sum(r => r.SignValue)
            })
            .OrderByDescending(x => x.RatingSum)
            .Take(1)
            .Join(_dbContext.Posts.Include(p => p.Author).Include(p => p.Room).ThenInclude(r => r.Game),
                x => x.PostId,
                p => p.PostId,
                (x, p) => new
                {
                    x.PostId,
                    x.GameId,
                    GameTitle = p.Room.Game.Title,
                    AuthorUsername = p.Author.Username,
                    x.RatingSum
                })
            .FirstOrDefaultAsync();

        return new LiveStats
        {
            Online = onlineCount,
            Totals = new TotalsWithDelta
            {
                Users = new StatValue { Value = totalUsers, TodayDelta = usersToday },
                Characters = new StatValue { Value = totalCharacters, TodayDelta = charactersToday },
                Games = new StatValue { Value = totalGames, TodayDelta = gamesToday },
                GamePosts = new StatValue { Value = totalPosts, TodayDelta = postsToday },
                Blogs = new StatValue { Value = totalBlogs, TodayDelta = blogsToday },
                Publications = new StatValue { Value = totalPublications, TodayDelta = publicationsToday }
            },
            WeeklyBestPost = weeklyBest != null && weeklyBest.RatingSum > 0
                ? new PostHighlight
                {
                    PostId = weeklyBest.PostId,
                    GameId = weeklyBest.GameId,
                    GameTitle = weeklyBest.GameTitle,
                    AuthorUsername = weeklyBest.AuthorUsername,
                    RatingSum = weeklyBest.RatingSum
                }
                : null
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<Leaderboards>> GetLeaderboards(int year, int? month)
    {
        var leaderboards = await _cache.GetOrCreateAsync($"Leaderboards_{year}_{month}", async entry =>
        {
            var (_, endDate) = ResolvePeriodBounds(year, month);
            entry.AbsoluteExpirationRelativeToNow = endDate <= _dateTimeProvider.Now
                ? ClosedPeriodCacheDuration
                : OpenPeriodCacheDuration;
            return await CalculateLeaderboards(year, month);
        });

        return new Envelope<Leaderboards>(leaderboards!);
    }

    private async Task<Leaderboards> CalculateLeaderboards(int year, int? month)
    {
        // year == 0 is the "all-time" period: aggregate across the full data
        // set with no date bounds. Otherwise a calendar year or month window.
        //
        // Contract: every board carries POSITIVE scores only (the "only
        // positive achievement is celebrated" product rule), enforced here so
        // no consumer has to re-filter. The SQL tie-break on the entity id
        // makes the Take(10) boundary deterministic across runs; the display
        // ranks (with ties sharing a rank) are assigned in memory below.
        var (startDate, endDate) = ResolvePeriodBounds(year, month);

        // Top players by rating received (sum of post review ratings)
        var topByRating = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .GroupBy(r => r.PostAuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(r => (int)r.SignValue) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(10)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync();

        // Top players by posts count
        var topByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(10)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync();

        // Top games by rating (sum of post review ratings)
        var topGamesByRating = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Score = g.Sum(r => (int)r.SignValue) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.GameId)
            .Take(10)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                PublicId = g.PublicId,
                Name = g.Title,
                Score = x.Score
            })
            .ToListAsync();

        // Top games by posts
        var topGamesByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.Room.GameId)
            .Select(g => new { GameId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.GameId)
            .Take(10)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                PublicId = g.PublicId,
                Name = g.Title,
                Score = x.Score
            })
            .ToListAsync();

        // Top players by written text volume ("Самый многопишущий игрок"):
        // the total number of in-character (GameText) characters authored in
        // the period. Score is the character count.
        var topByVolume = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(p => (int)p.GameText.Length) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(10)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync();

        // Top blogs by rating (blog analog of TopGamesByRating). Blog publications
        // are rated via the polymorphic Likes table (LikeEntityType.Publication),
        // so a blog's rating is the number of likes received on its publications.
        // Likes carry no timestamp, so the period is scoped by the publication's
        // CreatedUtc (same window as the publications count board below). Drives
        // from the period-filtered publications and inner-joins likes, so removed
        // or non-publication likes are excluded and there is no cartesian product.
        var topBlogsByRating = await _dbContext.Publications
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .Join(_dbContext.Likes.Where(l => !l.IsRemoved && l.EntityType == LikeEntityType.Publication),
                p => p.PublicationId,
                l => l.EntityId,
                (p, l) => p.BlogId)
            .GroupBy(blogId => blogId)
            .Select(g => new { BlogId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.BlogId)
            .Take(10)
            .Join(_dbContext.Blogs, x => x.BlogId, b => b.BlogId, (x, b) => new LeaderboardEntry
            {
                EntityId = b.BlogId,
                PublicId = b.PublicId,
                Name = b.Title,
                Score = x.Score
            })
            .ToListAsync();

        // Top blogs by publications count (blog analog of TopGamesByPosts).
        var topBlogsByPosts = await _dbContext.Publications
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.BlogId)
            .Select(g => new { BlogId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.BlogId)
            .Take(10)
            .Join(_dbContext.Blogs, x => x.BlogId, b => b.BlogId, (x, b) => new LeaderboardEntry
            {
                EntityId = b.BlogId,
                PublicId = b.PublicId,
                Name = b.Title,
                Score = x.Score
            })
            .ToListAsync();

        // Top blog authors by written text volume ("Самый многопишущий блогер"):
        // the blog analog of TopPlayersByVolume. Users are ranked by the total
        // number of characters in their blog publications' Content authored in
        // the period, grouped by the publication's AuthorId. Uses the same volume
        // definition (Content character length) as the player board for
        // consistency. Score is the character count.
        var topBlogAuthorsByVolume = await _dbContext.Publications
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(p => (int)p.Content.Length) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(10)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync();

        AssignCompetitionRanks(topByRating);
        AssignCompetitionRanks(topByPosts);
        AssignCompetitionRanks(topGamesByRating);
        AssignCompetitionRanks(topGamesByPosts);
        AssignCompetitionRanks(topByVolume);
        AssignCompetitionRanks(topBlogsByRating);
        AssignCompetitionRanks(topBlogsByPosts);
        AssignCompetitionRanks(topBlogAuthorsByVolume);

        return new Leaderboards
        {
            Period = new Period { Year = year, Month = month },
            TopPlayersByRating = topByRating.ToArray(),
            TopPlayersByPosts = topByPosts.ToArray(),
            TopGamesByRating = topGamesByRating.ToArray(),
            TopGamesByPosts = topGamesByPosts.ToArray(),
            TopPlayersByVolume = topByVolume.ToArray(),
            TopBlogsByRating = topBlogsByRating.ToArray(),
            TopBlogsByPosts = topBlogsByPosts.ToArray(),
            TopBlogAuthorsByVolume = topBlogAuthorsByVolume.ToArray()
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
