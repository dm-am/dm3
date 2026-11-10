using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Leaderboard = System.Collections.Generic.List<DM.Domain.Community.Features.Statistics.LeaderboardEntry>;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class CommunityStatsRepository : ICommunityStatsRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    public CommunityStatsRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<LiveStats> GetLiveStats(CancellationToken ct = default)
    {
        var now = _dateTimeProvider.Now;
        var todayStart = now.Date;
        var todayStartUtc = new DateTimeOffset(todayStart, TimeSpan.Zero);
        var onlineThreshold = now - ActivityPolicy.OnlinePeriod;
        var weekAgo = now.AddDays(-7);

        var onlineCount = await _dbContext.Users.CountAsync(u =>
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold, ct);

        var totals = await Totals(todayStartUtc, ct);
        var weeklyBestPost = await WeeklyBestPost(weekAgo, ct);

        return new LiveStats
        {
            Online = onlineCount,
            Totals = totals,
            WeeklyBestPost = weeklyBestPost
        };
    }

    private async Task<TotalsWithDelta> Totals(DateTimeOffset todayStartUtc, CancellationToken ct)
    {
        var totalUsers = await _dbContext.Users.CountAsync(ct);
        var usersToday = await _dbContext.Users.CountAsync(u => u.CreatedUtc >= todayStartUtc, ct);

        var totalCharacters = await _dbContext.Characters.CountAsync(ct);
        var charactersToday = await _dbContext.Characters.CountAsync(c => c.CreatedUtc >= todayStartUtc, ct);

        var totalGames = await _dbContext.Games.CountAsync(ct);
        var gamesToday = await _dbContext.Games.CountAsync(g => g.CreatedUtc >= todayStartUtc, ct);

        var totalPosts = await _dbContext.Posts.CountAsync(ct);
        var postsToday = await _dbContext.Posts.CountAsync(p => p.CreatedUtc >= todayStartUtc, ct);

        // Count all blogs (including drafts and private)
        var totalBlogs = await _dbContext.Blogs.CountAsync(ct);
        var blogsToday = await _dbContext.Blogs.CountAsync(b => b.CreatedUtc >= todayStartUtc, ct);

        var totalPublications = await _dbContext.Publications.CountAsync(ct);
        var publicationsToday = await _dbContext.Publications.CountAsync(p => p.CreatedUtc >= todayStartUtc, ct);

        return new TotalsWithDelta
        {
            Users = new StatValue { Value = totalUsers, TodayDelta = usersToday },
            Characters = new StatValue { Value = totalCharacters, TodayDelta = charactersToday },
            Games = new StatValue { Value = totalGames, TodayDelta = gamesToday },
            GamePosts = new StatValue { Value = totalPosts, TodayDelta = postsToday },
            Blogs = new StatValue { Value = totalBlogs, TodayDelta = blogsToday },
            Publications = new StatValue { Value = totalPublications, TodayDelta = publicationsToday }
        };
    }

    /// <summary>
    /// Weekly best post (highest total rating from post reviews), among the
    /// posts a guest can read: the block names the post, its game and author.
    /// None when the best of the week is not rated above zero.
    /// </summary>
    private async Task<PostHighlight?> WeeklyBestPost(DateTimeOffset weekAgo, CancellationToken ct)
    {
        var publicPosts = PublicPosts();
        var weeklyBest = await _dbContext.PostReviews
            .Where(r => r.CreatedUtc >= weekAgo)
            .Where(r => publicPosts.Any(p => p.PostId == r.PostId))
            .GroupBy(r => new { r.PostId, r.GameId })
            .Select(g => new
            {
                PostId = g.Key.PostId,
                GameId = g.Key.GameId,
                RatingSum = g.Sum(r => r.SignValue)
            })
            .OrderByDescending(x => x.RatingSum)
            .Take(1)
            // No Include on the join source: the result is an anonymous projection, and EF
            // drops it there for the same reason it drops one before a Select.
            .Join(_dbContext.Posts,
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

        return weeklyBest != null && weeklyBest.RatingSum > 0
            ? new PostHighlight
            {
                PostId = weeklyBest.PostId,
                GameId = weeklyBest.GameId,
                GameTitle = weeklyBest.GameTitle,
                AuthorUsername = weeklyBest.AuthorUsername,
                RatingSum = weeklyBest.RatingSum
            }
            : null;
    }

    /// <inheritdoc />
    public async Task<Leaderboards> GetLeaderboards(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default)
    {
        var players = await PlayerBoards(startDate, endDate, ct);
        var games = await GameBoards(startDate, endDate, ct);
        var blogs = await BlogBoards(startDate, endDate, ct);
        var blogAuthorsByVolume = await TopBlogAuthorsByVolume(startDate, endDate, ct);

        return new Leaderboards
        {
            TopPlayersByRating = players.ByRating,
            TopPlayersByPosts = players.ByPosts,
            TopGamesByRating = games.ByRating,
            TopGamesByPosts = games.ByPosts,
            TopPlayersByVolume = players.ByVolume,
            TopBlogsByRating = blogs.ByRating,
            TopBlogsByPosts = blogs.ByPosts,
            TopBlogAuthorsByVolume = blogAuthorsByVolume
        };
    }

    private async Task<(Leaderboard ByRating, Leaderboard ByPosts, Leaderboard ByVolume)> PlayerBoards(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
    {
        var topByRating = await UserBoard(_dbContext.PostReviews
            .Where(r => r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .GroupBy(r => r.PostAuthorId)
            .Select(g => new Scored { Id = g.Key, Score = g.Sum(r => (int)r.SignValue) }), ct);

        var topByPosts = await UserBoard(_dbContext.Posts
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new Scored { Id = g.Key, Score = g.Count() }), ct);

        // Counted on the visible text (SearchText), so markup and hidden blocks do not score.
        var topByVolume = await UserBoard(_dbContext.Posts
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new Scored { Id = g.Key, Score = g.Sum(p => (int)EF.Property<string>(p, "SearchText").Length) }), ct);

        return (topByRating, topByPosts, topByVolume);
    }

    private async Task<(Leaderboard ByRating, Leaderboard ByPosts)> GameBoards(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
    {
        // The boards name and link the modules, so they rank only what a guest can open.
        var publicGames = PublicGames();

        var topGamesByRating = await GameBoard(_dbContext.PostReviews
            .Where(r => r.CreatedUtc >= startDate && r.CreatedUtc < endDate)
            .Where(r => publicGames.Any(g => g.GameId == r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new Scored { Id = g.Key, Score = g.Sum(r => (int)r.SignValue) }), ct);

        var topGamesByPosts = await GameBoard(_dbContext.Posts
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .Where(p => publicGames.Any(g => g.GameId == p.Room.GameId))
            .GroupBy(p => p.Room.GameId)
            .Select(g => new Scored { Id = g.Key, Score = g.Count() }), ct);

        return (topGamesByRating, topGamesByPosts);
    }

    private async Task<(Leaderboard ByRating, Leaderboard ByPosts)> BlogBoards(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
    {
        var publicBlogs = PublicBlogs();

        // A blog's rating is the likes on its publications; likes carry no timestamp, so the
        // window is the publication's. Driven from publications, so removed likes drop out.
        var topBlogsByRating = await BlogBoard(_dbContext.Publications
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .Where(p => publicBlogs.Any(b => b.BlogId == p.BlogId))
            .Join(_dbContext.Likes.Where(l => l.EntityType == LikeEntityType.Publication),
                p => p.PublicationId,
                l => l.EntityId,
                (p, l) => p.BlogId)
            .GroupBy(blogId => blogId)
            .Select(g => new Scored { Id = g.Key, Score = g.Count() }), ct);

        var topBlogsByPosts = await BlogBoard(_dbContext.Publications
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .Where(p => publicBlogs.Any(b => b.BlogId == p.BlogId))
            .GroupBy(p => p.BlogId)
            .Select(g => new Scored { Id = g.Key, Score = g.Count() }), ct);

        return (topBlogsByRating, topBlogsByPosts);
    }

    /// <remarks>
    /// Counted on the source, unlike the player board: a publication has no projected
    /// text beside it, so its markup scores.
    /// </remarks>
    private Task<Leaderboard> TopBlogAuthorsByVolume(
        DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
        UserBoard(_dbContext.Publications
            .Where(p => p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new Scored { Id = g.Key, Score = g.Sum(p => (int)p.Content.Length) }), ct);

    private sealed class Scored
    {
        public Guid Id { get; set; }
        public int Score { get; set; }
    }

    private static IQueryable<Scored> Top(IQueryable<Scored> scores) =>
        scores
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id)
            .Take(Leaderboards.BoardSize);

    private Task<Leaderboard> UserBoard(IQueryable<Scored> scores, CancellationToken ct) =>
        Top(scores)
            .Join(_dbContext.Users, x => x.Id, u => u.UserId, (x, u) => new LeaderboardEntry
            {
                EntityId = u.UserId,
                Name = u.Username,
                Score = x.Score
            })
            .ToListAsync(ct);

    private Task<Leaderboard> GameBoard(IQueryable<Scored> scores, CancellationToken ct) =>
        Top(scores)
            .Join(_dbContext.Games, x => x.Id, g => g.GameId, (x, g) => new LeaderboardEntry
            {
                EntityId = g.GameId,
                PublicId = g.PublicId,
                Name = g.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

    private Task<Leaderboard> BlogBoard(IQueryable<Scored> scores, CancellationToken ct) =>
        Top(scores)
            .Join(_dbContext.Blogs, x => x.Id, b => b.BlogId, (x, b) => new LeaderboardEntry
            {
                EntityId = b.BlogId,
                PublicId = b.PublicId,
                Name = b.Title,
                Score = x.Score
            })
            .ToListAsync(ct);

    /// <summary>
    /// The games a guest can open, by the filter every read of a game applies to a
    /// reader holding no role (an empty reader id matches no role arm).
    /// </summary>
    private IQueryable<DbGame> PublicGames() =>
        _dbContext.Games.Where(GameAccessibilityFilters.GameAvailable(Guid.Empty));

    private IQueryable<DbPost> PublicPosts() =>
        _dbContext.Rooms
            .Where(GameAccessibilityFilters.RoomAvailable(Guid.Empty))
            .SelectMany(room => room.Posts);

    /// <summary>
    /// The blogs a guest can open: the rule the blog list applies, approved and
    /// not a private draft.
    /// </summary>
    private IQueryable<DbBlog> PublicBlogs() =>
        _dbContext.Blogs.Where(BlogAccessibilityFilters.PubliclyVisible);
}
