using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class CommunityStatsRepository : ICommunityStatsRepository
{
    private readonly DmDbContext _dbContext;

    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public CommunityStatsRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LiveStats> GetLiveStats(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = now.Date;
        var todayStartUtc = new DateTimeOffset(todayStart, TimeSpan.Zero);
        var onlineThreshold = now - OnlineThreshold;
        var weekAgo = now.AddDays(-7);

        // Online users count
        var onlineCount = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  &&
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold, ct);

        // Totals with today's delta
        var totalUsers = await _dbContext.Users.CountAsync(u => !u.IsRemoved , ct);
        var usersToday = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved  && u.CreatedUtc >= todayStartUtc, ct);

        var totalCharacters = await _dbContext.Characters.CountAsync(c => !c.IsRemoved, ct);
        var charactersToday = await _dbContext.Characters.CountAsync(c =>
            !c.IsRemoved && c.CreatedUtc >= todayStartUtc, ct);

        var totalGames = await _dbContext.Games.CountAsync(g => !g.IsRemoved, ct);
        var gamesToday = await _dbContext.Games.CountAsync(g =>
            !g.IsRemoved && g.CreatedUtc >= todayStartUtc, ct);

        var totalPosts = await _dbContext.Posts.CountAsync(p => !p.IsRemoved, ct);
        var postsToday = await _dbContext.Posts.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc >= todayStartUtc, ct);

        // Count all blogs (including drafts and private)
        var totalBlogs = await _dbContext.Blogs.CountAsync(b => !b.IsRemoved, ct);
        var blogsToday = await _dbContext.Blogs.CountAsync(b =>
            !b.IsRemoved && b.CreatedUtc >= todayStartUtc, ct);

        // Count all publications (including unpublished drafts)
        var totalPublications = await _dbContext.Publications.CountAsync(p => !p.IsRemoved, ct);
        var publicationsToday = await _dbContext.Publications.CountAsync(p =>
            !p.IsRemoved && p.CreatedUtc >= todayStartUtc, ct);

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
            .FirstOrDefaultAsync(ct);

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
    public async Task<LeaderboardBoards> GetLeaderboards(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default)
    {
        // Top players by rating received (sum of post review ratings)
        var topByRating = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .GroupBy(r => r.PostAuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(r => (int)r.SignValue) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync(ct);

        // Top players by posts count
        var topByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync(ct);

        // Top games by rating (sum of post review ratings)
        var topGamesByRating = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Score = g.Sum(r => (int)r.SignValue) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.GameId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                PublicId = g.PublicId,
                Name = g.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

        // Top games by posts
        var topGamesByPosts = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.Room.GameId)
            .Select(g => new { GameId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.GameId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Games, x => x.GameId, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                PublicId = g.PublicId,
                Name = g.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

        // Top players by written text volume ("Самый многопишущий игрок"):
        // the total number of in-character (GameText) characters authored in
        // the period. Score is the character count.
        var topByVolume = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(p => (int)p.GameText.Length) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync(ct);

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
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Blogs, x => x.BlogId, b => b.BlogId, (x, b) => new LeaderboardEntry
            {
                EntityId = b.BlogId,
                PublicId = b.PublicId,
                Name = b.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

        // Top blogs by publications count (blog analog of TopGamesByPosts).
        var topBlogsByPosts = await _dbContext.Publications
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.BlogId)
            .Select(g => new { BlogId = g.Key, Score = g.Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.BlogId)
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Blogs, x => x.BlogId, b => b.BlogId, (x, b) => new LeaderboardEntry
            {
                EntityId = b.BlogId,
                PublicId = b.PublicId,
                Name = b.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

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
            .Take(LeaderboardBoards.BoardSize)
            .Join(_dbContext.Users, x => x.UserId, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync(ct);

        return new LeaderboardBoards
        {
            TopPlayersByRating = topByRating,
            TopPlayersByPosts = topByPosts,
            TopGamesByRating = topGamesByRating,
            TopGamesByPosts = topGamesByPosts,
            TopPlayersByVolume = topByVolume,
            TopBlogsByRating = topBlogsByRating,
            TopBlogsByPosts = topBlogsByPosts,
            TopBlogAuthorsByVolume = topBlogAuthorsByVolume
        };
    }
}
