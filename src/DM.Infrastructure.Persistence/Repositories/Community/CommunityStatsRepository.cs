using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class CommunityStatsRepository : ICommunityStatsRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
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
        var onlineThreshold = now - OnlineThreshold;
        var weekAgo = now.AddDays(-7);

        // Online users count
        var onlineCount = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved &&
            u.LastActivityUtc.HasValue && u.LastActivityUtc.Value > onlineThreshold, ct);

        // Totals with today's delta
        var totalUsers = await _dbContext.Users.CountAsync(u => !u.IsRemoved, ct);
        var usersToday = await _dbContext.Users.CountAsync(u =>
            !u.IsRemoved && u.CreatedUtc >= todayStartUtc, ct);

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
        // how much text the player wrote in the period, counted on the visible
        // text of the post rather than on its source.
        //
        // The source was the wrong string on a public board, in two ways that
        // point the same direction. Markup counted: a post wrapped in tags scored
        // for characters no reader ever sees, so the same paragraph could outscore
        // itself by being formatted. And [private] counted: its whole point is
        // that the room does not see it, and it was adding to a number the room
        // is shown - a player could take the top place with text nobody could
        // read, and nobody could tell from the board that they had.
        //
        // SearchText is the plain-text render written beside the body, the same
        // projection the search index and the post filter read: markup gone,
        // hidden blocks filtered out as tree nodes rather than cut as strings,
        // runs of whitespace collapsed to one space. So the score is now the text
        // a reader of the room actually got, which is what the board claims to be
        // measuring. Numbers on it move down, and they move down most for the
        // authors who were furthest from the claim.
        var topByVolume = await _dbContext.Posts
            .Where(p => !p.IsRemoved && p.CreatedUtc >= startDate && p.CreatedUtc < endDate)
            .GroupBy(p => p.AuthorId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(p => (int)EF.Property<string>(p, "SearchText").Length) })
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
        // the period, grouped by the publication's AuthorId.
        //
        // Still counted on the source, unlike the player board above, and that is
        // a gap rather than a decision: markup counts here too, so a formatted
        // publication outscores the same words unformatted. The board cannot be
        // moved with it because a publication has no projected text beside it -
        // Message, Post, Topic and Comment do (see DmDbContext.ProjectedBodies),
        // Publication does not - and giving it one is a column, a migration and a
        // reseed rather than a line here. What does not apply is the other half:
        // [private] is a game-post tag, so no publication can score for text its
        // readers cannot see.
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
