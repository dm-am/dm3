using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Shared;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// A period the boards are read over: its first moment, the moment after its
    /// last, and whether it is the current month.
    /// </summary>
    private sealed record CoverageWindow(DateTimeOffset Start, DateTimeOffset End, bool IsCurrentMonth);

    /// <summary>
    /// Ensures every rating and blog leaderboard has a full top-10 for the periods the site shows by
    /// default: the current month and the closed periods of the digest topics (the last closed month
    /// and the previous year). The post and volume boards fill from organic data.
    /// </summary>
    /// <remarks>
    /// Idempotent per window: coverage is measured as the boards measure it, only the missing part is added.
    /// </remarks>
    private async Task EnsureLeaderboardCoverage(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var monthStart = new DateTimeOffset(_now.Year, _now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevYearDecStart = new DateTimeOffset(_now.Year - 1, 12, 1, 0, 0, 0, TimeSpan.Zero);

        // The homepage widgets stay on the organic showcase posts, and the weekly one ranks by the post date,
        // so coverage rates only posts created before the current week, and in the current month dates its
        // reviews before the week and at least six hours before now.
        var weekStart = WeekStartUtc(_now);

        var nonDraftGameIds = await _dbContext.Set<DbGame>()
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync();

        // Every pass draws from the generator, so the order of this list is part of the fixture.
        var blogs = await _dbContext.Set<DbBlog>()
            .Where(b => !b.IsRemoved && b.Status != ModuleStatus.Draft)
            .OrderBy(b => b.SerialNumber)
            .ToListAsync();

        // Publication numbers are assigned before a single SaveChanges, so the maximum is bumped in memory.
        var nextPublicationNumber = new Dictionary<Guid, int>();

        // A covered post belongs to one window: review dates stay inside it and (post, reviewer) is unique.
        var usedPostIds = new HashSet<Guid>();

        // Closed windows first, so that they claim their own posts.
        CoverageWindow[] windows =
        [
            new(prevMonthStart, monthStart, IsCurrentMonth: false),
            new(prevYearDecStart, prevYearDecStart.AddMonths(1), IsCurrentMonth: false),
            new(monthStart, monthStart.AddMonths(1), IsCurrentMonth: true),
        ];
        foreach (var window in windows)
        {
            await CoverRatingBoards(window, users, weekStart, nonDraftGameIds, usedPostIds, result);
            await CoverBlogBoards(window, users, blogs, nextPublicationNumber, result);
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add("Ensured full top-10 rating coverage for the current month, the last closed month and the previous year");
    }

    /// <summary>
    /// Rating boards: TopPlayersByRating + TopGamesByRating. Both sum
    /// PostReview.SignValue; the window is measured exactly as the boards do
    /// before topping up.
    /// </summary>
    private async Task CoverRatingBoards(
        CoverageWindow window, List<DbUser> users, DateTimeOffset weekStart,
        List<Guid> nonDraftGameIds, HashSet<Guid> usedPostIds, ComprehensiveSeedResult result)
    {
        var (winStart, winEnd, _) = window;

        // A little over the board size so each board is unambiguously full.
        const int target = Leaderboards.BoardSize + 2;

        var windowReviews = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved && r.CreatedUtc >= winStart && r.CreatedUtc < winEnd)
            .Select(r => new { r.GameId, r.PostAuthorId, Sign = (int)r.SignValue })
            .ToListAsync();
        var positiveGames = windowReviews.GroupBy(r => r.GameId).Count(g => g.Sum(x => x.Sign) > 0);
        var positiveAuthors = windowReviews.GroupBy(r => r.PostAuthorId).Count(g => g.Sum(x => x.Sign) > 0);

        if (positiveGames < Leaderboards.BoardSize || positiveAuthors < Leaderboards.BoardSize)
        {
            var picked = await PickCoveragePosts(window, weekStart, nonDraftGameIds, usedPostIds, target);

            // Reviewers who already reviewed a candidate post must not be
            // reused for it — (post, reviewer) stays unique.
            var pickedIds = picked.Select(x => x.PostId).ToList();
            var existingPairs = (await _dbContext.PostReviews
                    .Where(r => pickedIds.Contains(r.PostId))
                    .Select(r => new { r.PostId, r.AuthorId })
                    .ToListAsync())
                .Select(x => (x.PostId, x.AuthorId))
                .ToHashSet();

            // Descending scores, so the boards read as a ranking and not as ties.
            for (var i = 0; i < picked.Count; i++)
            {
                var post = picked[i];
                usedPostIds.Add(post.PostId);
                var score = Math.Max(1, target - i);
                var reviewers = users
                    .Where(u => u.UserId != post.AuthorId
                        && !existingPairs.Contains((post.PostId, u.UserId)))
                    .OrderBy(_ => _random.Next())
                    .Take(score)
                    .ToList();
                foreach (var reviewer in reviewers)
                {
                    AddPostReview(post.PostId, post.AuthorId, post.GameId, reviewer.UserId,
                        CoverageReviewDate(window, weekStart), "Отличный отыгрыш!", ReviewSign.Positive, result);
                }
            }
        }
    }

    /// <summary>
    /// The posts a window tops up, in the order of the ranking the boards end up
    /// showing: one post per distinct (game, author) first, so one batch lights
    /// up `target` distinct games AND `target` distinct authors.
    /// </summary>
    private async Task<List<(Guid PostId, Guid AuthorId, Guid GameId)>> PickCoveragePosts(
        CoverageWindow window, DateTimeOffset weekStart, List<Guid> nonDraftGameIds, HashSet<Guid> usedPostIds,
        int target)
    {
        var (winStart, winEnd, isCurrentMonth) = window;

        // Every window stops at the week start: a post of this week would outrank the showcase post.
        var candidatesQuery = _dbContext.Posts
            .Where(po => !po.IsRemoved
                && po.Room.AccessType == RoomAccessType.Open
                && nonDraftGameIds.Contains(po.Room.GameId)
                && po.CreatedUtc < weekStart);
        if (!isCurrentMonth)
        {
            candidatesQuery = candidatesQuery
                .Where(po => po.CreatedUtc >= winStart && po.CreatedUtc < winEnd);
        }
        // The first rows take the highest scores, so this order is the ranking the boards show.
        var candidates = (await candidatesQuery
                .OrderBy(po => po.CreatedUtc)
                .ThenBy(po => po.PostId)
                .Select(po => new { po.PostId, po.AuthorId, GameId = po.Room.GameId })
                .ToListAsync())
            .Where(c => !usedPostIds.Contains(c.PostId))
            .ToList();

        var seenGames = new HashSet<Guid>();
        var seenAuthors = new HashSet<Guid>();
        var picked = new List<(Guid PostId, Guid AuthorId, Guid GameId)>();
        foreach (var c in candidates)
        {
            if (picked.Count >= target) break;
            if (!seenGames.Add(c.GameId)) continue;
            if (!seenAuthors.Add(c.AuthorId)) { seenGames.Remove(c.GameId); continue; }
            picked.Add((c.PostId, c.AuthorId, c.GameId));
        }
        if (picked.Count < target)
        {
            foreach (var c in candidates)
            {
                if (picked.Count >= target) break;
                if (!seenGames.Add(c.GameId)) continue;
                picked.Add((c.PostId, c.AuthorId, c.GameId));
            }
        }

        return picked;
    }

    /// <summary>
    /// A moment inside the window for a coverage review. In the current month it
    /// stays before the week start and six hours before now, so the freshest
    /// review on the site remains the organic one.
    /// </summary>
    private DateTimeOffset CoverageReviewDate(CoverageWindow window, DateTimeOffset weekStart)
    {
        var (winStart, winEnd, isCurrentMonth) = window;

        var cap = isCurrentMonth ? NoLaterThan(weekStart, _now.AddHours(-6)) : winEnd;
        var minutes = (int)Math.Max(0, (cap - winStart).TotalMinutes);
        return winStart.AddMinutes(minutes == 0 ? 0 : _random.Next(0, minutes));
    }

    /// <summary>
    /// Blog boards: TopBlogsByRating (publication likes) + TopBlogsByPosts +
    /// TopBlogAuthorsByVolume. All three read publications dated in the window;
    /// every blog without one gets a publication (with a few likes).
    /// </summary>
    private async Task CoverBlogBoards(
        CoverageWindow window, List<DbUser> users, List<DbBlog> blogs, Dictionary<Guid, int> nextPublicationNumber,
        ComprehensiveSeedResult result)
    {
        var (winStart, winEnd, _) = window;

        var blogsWithWindowPub = (await _dbContext.Set<Publication>()
            .Where(pu => !pu.IsRemoved
                && pu.CreatedUtc >= winStart && pu.CreatedUtc < winEnd)
            .Select(pu => pu.BlogId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        foreach (var blog in blogs)
        {
            if (blogsWithWindowPub.Contains(blog.BlogId)) continue;

            var rubricId = await _dbContext.Set<Rubric>()
                .Where(r => r.BlogId == blog.BlogId && !r.IsRemoved)
                .OrderBy(r => r.SortOrder)
                .Select(r => (Guid?)r.RubricId)
                .FirstOrDefaultAsync();
            if (rubricId == null) continue;

            if (!nextPublicationNumber.TryGetValue(blog.BlogId, out var nextNumber))
            {
                nextNumber = (await _dbContext.Set<Publication>()
                    .Where(pu => pu.BlogId == blog.BlogId)
                    .MaxAsync(pu => (int?)pu.PublicationNumber) ?? 0) + 1;
            }
            nextPublicationNumber[blog.BlogId] = nextNumber + 1;

            const string content = "Заметка за месяц: что заметил в играх, что собираюсь "
                + "попробовать и пара историй с недавних сессий.";
            var windowCap = NoLaterThan(winEnd, _now);
            var windowMinutes = (int)Math.Max(1, (windowCap - winStart).TotalMinutes);
            var createdUtc = winStart.AddMinutes(_random.Next(0, windowMinutes));
            var publication = new Publication
            {
                PublicationId = _guidFactory.Create(),
                BlogId = blog.BlogId,
                AuthorId = blog.AuthorId,
                RubricId = rubricId.Value,
                PublicationNumber = nextNumber,
                Title = "Заметки месяца",
                Content = content,
                Preview = PreviewOf(content),
                CreatedUtc = createdUtc,
                ViewCount = _random.Next(10, 500),
            };
            _dbContext.Set<Publication>().Add(publication);
            blog.PublicationCount++;
            result.PublicationsCreated++;

            // At least one like, so TopBlogsByRating counts this blog too.
            result.LikesCreated += AddLikes(
                publication.PublicationId, LikeEntityType.Publication, blog.AuthorId, users, minimum: 1);
        }
    }

    /// <summary>
    /// Recalculates the popularity scores of the fixture through the same
    /// processors the running site uses, at the seed epoch.
    /// </summary>
    private async Task UpdatePopularityScores(ComprehensiveSeedResult result)
    {
        var (_, games) = await _gamePopularity.UpdateScoresAsync(_now);
        var (_, blogs) = await _blogPopularity.UpdateScoresAsync(_now);

        result.Details.Add($"Updated popularity scores for {games} games and {blogs} blogs");
    }

    private async Task CreateLikes(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var existingLikesCount = await _dbContext.Set<Like>().CountAsync();
        if (existingLikesCount > 0)
        {
            result.Details.Add($"Likes already exist ({existingLikesCount}), skipping");
            return;
        }

        var likesCreated = 0;

        // Forum topics
        var topics = await _dbContext.Set<Topic>()
            .Where(t => !t.IsRemoved)
            .Select(t => new { t.TopicId, t.AuthorId })
            .ToListAsync();
        foreach (var topic in topics)
        {
            likesCreated += AddLikes(topic.TopicId, LikeEntityType.Topic, topic.AuthorId, users);
        }

        // Forum and publication comments
        var comments = await _dbContext.Set<DbComment>()
            .Where(c => !c.IsRemoved)
            .Select(c => new { c.CommentId, c.AuthorId })
            .ToListAsync();
        foreach (var comment in comments)
        {
            likesCreated += AddLikes(comment.CommentId, LikeEntityType.Comment, comment.AuthorId, users);
        }

        // Publications
        var publications = await _dbContext.Set<Publication>()
            .Where(p => !p.IsRemoved)
            .Select(p => new { p.PublicationId, p.AuthorId })
            .ToListAsync();
        foreach (var publication in publications)
        {
            likesCreated += AddLikes(publication.PublicationId, LikeEntityType.Publication, publication.AuthorId, users);
        }

        // Chat messages, the global chat and private and group chats
        var messages = await _dbContext.Set<Message>()
            .Where(m => !m.IsRemoved)
            .Select(m => new { m.MessageId, m.UserId })
            .ToListAsync();
        foreach (var message in messages)
        {
            likesCreated += AddLikes(message.MessageId, LikeEntityType.Message, message.UserId, users);
        }

        await _dbContext.SaveChangesAsync();

        result.LikesCreated = likesCreated;
        result.Details.Add($"Created {likesCreated} likes");
    }

    /// <summary>
    /// Random likes on one entity, from users other than its author.
    /// </summary>
    /// <returns>How many likes it got.</returns>
    private int AddLikes(Guid entityId, LikeEntityType entityType, Guid? authorId, List<DbUser> users, int minimum = 0)
    {
        var likesCreated = 0;

        var likeCount = _random.Next(minimum, 6);
        var eligibleUsers = authorId.HasValue
            ? users.Where(u => u.UserId != authorId.Value).ToList()
            : users;

        var likers = eligibleUsers
            .OrderBy(_ => _random.Next())
            .Take(likeCount)
            .ToList();

        foreach (var liker in likers)
        {
            _dbContext.Set<Like>().Add(new Like
            {
                LikeId = _guidFactory.Create(),
                EntityId = entityId,
                EntityType = entityType,
                UserId = liker.UserId,
            });
            likesCreated++;
        }

        return likesCreated;
    }
}
