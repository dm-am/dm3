using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Web.API.Dto.Community;
using DM.Web.API.Dto.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DM.Web.API.Services.Community;

/// <inheritdoc />
internal class CommunityStatsService : ICommunityStatsService
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMemoryCache _cache;

    private const string CacheKey = "CommunityStats";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public CommunityStatsService(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Envelope<CommunityStats>> GetStats()
    {
        var stats = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await CalculateStats();
        });

        return new Envelope<CommunityStats>(stats!);
    }

    private async Task<CommunityStats> CalculateStats()
    {
        var now = _dateTimeProvider.Now;
        var monthAgo = now.AddMonths(-1);
        var yearAgo = now.AddYears(-1);
        var twoYearsAgo = now.AddYears(-2);
        var onlineThreshold = now - OnlineThreshold;

        // Current stats
        var totalUsersTask = _dbContext.Users.CountAsync(u => !u.IsRemoved );
        var onlineUsersTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  &&
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold);
        var activeGamesTask = _dbContext.Games.CountAsync(g => !g.IsRemoved && g.Status == ModuleStatus.Active);
        var totalGamesTask = _dbContext.Games.CountAsync(g => !g.IsRemoved);
        var totalPostsTask = _dbContext.Posts.CountAsync(p => !p.IsRemoved);
        var totalTopicsTask = _dbContext.Topics.CountAsync(t => !t.IsRemoved);

        // Monthly stats
        var newUsersMonthTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc > monthAgo);
        var newGamesMonthTask = _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc > monthAgo);
        var newPostsMonthTask = _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc > monthAgo);
        var newTopicsMonthTask = _dbContext.Topics.CountAsync(t =>
            !t.IsRemoved && t.CreatedUtc > monthAgo);
        var newCommentsMonthTask = _dbContext.Comments.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc > monthAgo);

        // Yearly stats
        var newUsersYearTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc > yearAgo);
        var newGamesYearTask = _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc > yearAgo);
        var newPostsYearTask = _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc > yearAgo);
        var newTopicsYearTask = _dbContext.Topics.CountAsync(t =>
            !t.IsRemoved && t.CreatedUtc > yearAgo);
        var newCommentsYearTask = _dbContext.Comments.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc > yearAgo);

        // Previous year stats for comparison
        var usersLastYearTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc > twoYearsAgo && u.CreatedUtc <= yearAgo);
        var gamesLastYearTask = _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc > twoYearsAgo && g.CreatedUtc <= yearAgo);
        var postsLastYearTask = _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc > twoYearsAgo && p.CreatedUtc <= yearAgo);

        // Await all tasks
        await Task.WhenAll(
            totalUsersTask, onlineUsersTask, activeGamesTask, totalGamesTask, totalPostsTask, totalTopicsTask,
            newUsersMonthTask, newGamesMonthTask, newPostsMonthTask, newTopicsMonthTask, newCommentsMonthTask,
            newUsersYearTask, newGamesYearTask, newPostsYearTask, newTopicsYearTask, newCommentsYearTask,
            usersLastYearTask, gamesLastYearTask, postsLastYearTask);

        var newUsersYear = await newUsersYearTask;
        var newGamesYear = await newGamesYearTask;
        var newPostsYear = await newPostsYearTask;
        var usersLastYear = await usersLastYearTask;
        var gamesLastYear = await gamesLastYearTask;
        var postsLastYear = await postsLastYearTask;

        return new CommunityStats
        {
            Current = new CurrentStats
            {
                TotalUsers = await totalUsersTask,
                OnlineUsers = await onlineUsersTask,
                ActiveGames = await activeGamesTask,
                TotalGames = await totalGamesTask,
                TotalPosts = await totalPostsTask,
                TotalTopics = await totalTopicsTask
            },
            Monthly = new PeriodStats
            {
                NewUsers = await newUsersMonthTask,
                NewGames = await newGamesMonthTask,
                NewPosts = await newPostsMonthTask,
                NewTopics = await newTopicsMonthTask,
                NewComments = await newCommentsMonthTask
            },
            Yearly = new PeriodStats
            {
                NewUsers = newUsersYear,
                NewGames = newGamesYear,
                NewPosts = newPostsYear,
                NewTopics = await newTopicsYearTask,
                NewComments = await newCommentsYearTask
            },
            Comparison = new YearComparison
            {
                UsersGrowth = CalculateGrowth(newUsersYear, usersLastYear),
                GamesGrowth = CalculateGrowth(newGamesYear, gamesLastYear),
                PostsGrowth = CalculateGrowth(newPostsYear, postsLastYear)
            }
        };
    }

    private static double CalculateGrowth(int current, int previous)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round((current - previous) / (double)previous * 100, 1);
    }

    /// <inheritdoc />
    public async Task<Envelope<LiveStats>> GetLiveStats()
    {
        var liveStats = await _cache.GetOrCreateAsync("LiveStats", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
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

        // Weekly best post (highest total rating from post reviews)
        var weeklyBest = await _dbContext.Reviews
            .Where(r => !r.IsRemoved && r.TargetType == ReviewTargetType.Post && r.CreatedUtc >= weekAgo)
            .GroupBy(r => new { r.TargetId, r.GameId })
            .Select(g => new
            {
                PostId = g.Key.TargetId!.Value,
                GameId = g.Key.GameId!.Value,
                RatingSum = g.Sum(r => r.SignValue ?? 0)
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
                    AuthorLogin = p.Author.Login,
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
                GamePosts = new StatValue { Value = totalPosts, TodayDelta = postsToday }
            },
            WeeklyBestPost = weeklyBest != null && weeklyBest.RatingSum > 0
                ? new PostHighlight
                {
                    PostId = weeklyBest.PostId,
                    GameId = weeklyBest.GameId,
                    GameTitle = weeklyBest.GameTitle,
                    AuthorLogin = weeklyBest.AuthorLogin,
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
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await CalculateLeaderboards(year, month);
        });

        return new Envelope<Leaderboards>(leaderboards!);
    }

    private async Task<Leaderboards> CalculateLeaderboards(int year, int? month)
    {
        DateTimeOffset startDate, endDate;
        if (month.HasValue)
        {
            startDate = new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero);
            endDate = startDate.AddMonths(1);
        }
        else
        {
            startDate = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            endDate = startDate.AddYears(1);
        }

        // Top players by rating received (sum of post review ratings)
        var topByRating = await _dbContext.Reviews
            .Where(r => !r.IsRemoved && r.TargetType == ReviewTargetType.Post
                && r.CreatedUtc >= startDate && r.CreatedUtc < endDate && r.PostAuthorId.HasValue)
            .GroupBy(r => r.PostAuthorId!.Value)
            .Select(g => new { UserId = g.Key, TotalRating = g.Sum(r => (int)(r.SignValue ?? 0)) })
            .OrderByDescending(x => x.TotalRating)
            .Take(10)
            .Join(_dbContext.Users.Include(u => u.AvatarUpload), x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Login,
                PictureUrl = u.AvatarUpload != null ? (u.AvatarUpload.SmallFilePath ?? u.AvatarUpload.FilePath) : null,
                Score = x.TotalRating
            })
            .ToListAsync();

        // Add ranks
        for (int i = 0; i < topByRating.Count; i++)
            topByRating[i].Rank = i + 1;

        // Top players by posts count
        var topByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(10)
            .Join(_dbContext.Users.Include(u => u.AvatarUpload), x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Login,
                PictureUrl = u.AvatarUpload != null ? (u.AvatarUpload.SmallFilePath ?? u.AvatarUpload.FilePath) : null,
                Score = x.PostCount
            })
            .ToListAsync();

        for (int i = 0; i < topByPosts.Count; i++)
            topByPosts[i].Rank = i + 1;

        // Top games by rating (sum of post review ratings)
        var topGamesByRating = await _dbContext.Reviews
            .Where(r => !r.IsRemoved && r.TargetType == ReviewTargetType.Post
                && r.CreatedUtc >= startDate && r.CreatedUtc < endDate && r.GameId.HasValue)
            .GroupBy(r => r.GameId!.Value)
            .Select(g => new { GameId = g.Key, TotalRating = g.Sum(r => (int)(r.SignValue ?? 0)) })
            .OrderByDescending(x => x.TotalRating)
            .Take(10)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                Name = g.Title,
                Score = x.TotalRating
            })
            .ToListAsync();

        for (int i = 0; i < topGamesByRating.Count; i++)
            topGamesByRating[i].Rank = i + 1;

        // Top games by posts
        var topGamesByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.Room.GameId)
            .Select(g => new { GameId = g.Key, PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(10)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                Name = g.Title,
                Score = x.PostCount
            })
            .ToListAsync();

        for (int i = 0; i < topGamesByPosts.Count; i++)
            topGamesByPosts[i].Rank = i + 1;

        return new Leaderboards
        {
            Period = new Period { Year = year, Month = month },
            TopPlayersByRating = topByRating.ToArray(),
            TopPlayersByPosts = topByPosts.ToArray(),
            TopGamesByRating = topGamesByRating.ToArray(),
            TopGamesByPosts = topGamesByPosts.ToArray()
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<PeriodReport>> GetPeriodReport(int year, int? month)
    {
        var report = await _cache.GetOrCreateAsync($"PeriodReport_{year}_{month}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await CalculatePeriodReport(year, month);
        });

        return new Envelope<PeriodReport>(report!);
    }

    private async Task<PeriodReport> CalculatePeriodReport(int year, int? month)
    {
        DateTimeOffset startDate, endDate;
        if (month.HasValue)
        {
            startDate = new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero);
            endDate = startDate.AddMonths(1);
        }
        else
        {
            startDate = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            endDate = startDate.AddYears(1);
        }

        var registrations = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc >= startDate && u.CreatedUtc < endDate);

        var gamesCreated = await _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc >= startDate && g.CreatedUtc < endDate);

        var gamePosts = await _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate);

        var reviews = await _dbContext.Reviews.CountAsync(r =>
            !r.IsRemoved && r.CreatedUtc >= startDate && r.CreatedUtc < endDate);

        // Unique active users (posted at least once)
        var activeUsers = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .Select(p => p.UserId)
            .Distinct()
            .CountAsync();

        return new PeriodReport
        {
            Period = new Period { Year = year, Month = month },
            Registrations = registrations,
            GamesCreated = gamesCreated,
            GamePosts = gamePosts,
            Reviews = reviews,
            AverageDailyUsers = activeUsers
        };
    }

    /// <inheritdoc />
    public async Task<Envelope<PeriodComparison>> ComparePeriods(int year1, int? month1, int year2, int? month2)
    {
        var report1Task = CalculatePeriodReport(year1, month1);
        var report2Task = CalculatePeriodReport(year2, month2);

        await Task.WhenAll(report1Task, report2Task);

        var report1 = await report1Task;
        var report2 = await report2Task;

        return new Envelope<PeriodComparison>(new PeriodComparison
        {
            Period1 = report1,
            Period2 = report2,
            Growth = new GrowthMetrics
            {
                RegistrationsGrowth = CalculateGrowth(report2.Registrations, report1.Registrations),
                GamesGrowth = CalculateGrowth(report2.GamesCreated, report1.GamesCreated),
                PostsGrowth = CalculateGrowth(report2.GamePosts, report1.GamePosts),
                ReviewsGrowth = CalculateGrowth(report2.Reviews, report1.Reviews)
            }
        });
    }
}
