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
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(30);

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

        return new Envelope<CommunityStats>(stats);
    }

    private async Task<CommunityStats> CalculateStats()
    {
        var now = _dateTimeProvider.Now;
        var monthAgo = now.AddMonths(-1);
        var yearAgo = now.AddYears(-1);
        var twoYearsAgo = now.AddYears(-2);
        var onlineThreshold = now - OnlineThreshold;

        // Current stats
        var totalUsersTask = _dbContext.Users.CountAsync(u => !u.IsRemoved && u.Activated);
        var onlineUsersTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved && u.Activated &&
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold);
        var activeGamesTask = _dbContext.Games.CountAsync(g => !g.IsRemoved && g.Status == GameStatus.Active);
        var totalGamesTask = _dbContext.Games.CountAsync(g => !g.IsRemoved);
        var totalPostsTask = _dbContext.Posts.CountAsync(p => !p.IsRemoved);
        var totalTopicsTask = _dbContext.ForumTopics.CountAsync(t => !t.IsRemoved);

        // Monthly stats
        var newUsersMonthTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved && u.Activated && u.CreatedUtc > monthAgo);
        var newGamesMonthTask = _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc > monthAgo);
        var newPostsMonthTask = _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc > monthAgo);
        var newTopicsMonthTask = _dbContext.ForumTopics.CountAsync(t =>
            !t.IsRemoved && t.CreatedUtc > monthAgo);
        var newCommentsMonthTask = _dbContext.Comments.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc > monthAgo);

        // Yearly stats
        var newUsersYearTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved && u.Activated && u.CreatedUtc > yearAgo);
        var newGamesYearTask = _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc > yearAgo);
        var newPostsYearTask = _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc > yearAgo);
        var newTopicsYearTask = _dbContext.ForumTopics.CountAsync(t =>
            !t.IsRemoved && t.CreatedUtc > yearAgo);
        var newCommentsYearTask = _dbContext.Comments.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc > yearAgo);

        // Previous year stats for comparison
        var usersLastYearTask = _dbContext.Users.CountAsync(u =>
            !u.IsRemoved && u.Activated && u.CreatedUtc > twoYearsAgo && u.CreatedUtc <= yearAgo);
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
}
