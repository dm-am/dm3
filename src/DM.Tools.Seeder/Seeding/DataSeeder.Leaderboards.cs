using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Ensures every statistics leaderboard has a full top-10 for the periods
    /// the site shows by default: the current month (/statistics landing) and
    /// the CLOSED periods the auto-created digest topics summarize — the last
    /// closed month and the previous year (December window). The organic seed
    /// leaves the rating boards short there: post reviews cluster in a single
    /// finished game and blog publications spread across ~1.5 years. The
    /// post/volume boards fill from organic data and are left untouched.
    ///
    /// Self-correcting and idempotent per window: coverage is measured the
    /// same way the boards measure it (a positive score inside the window)
    /// and only what is missing is added; reviewers who already reviewed a
    /// post are never reused for it, and each covered post is used by one
    /// window only.
    /// </summary>
    private async Task EnsureLeaderboardCoverage(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // A little over the board size so each board is unambiguously full.
        const int target = LeaderboardBoards.BoardSize + 2;

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevYearDecStart = new DateTimeOffset(now.Year - 1, 12, 1, 0, 0, 0, TimeSpan.Zero);

        // The homepage widgets must stay on the organic showcase posts (Chuck's
        // grapefruit post for "лучший пост недели", the fireplace post for
        // "последний оцененный"). The weekly widget ranks by the POST's creation
        // date, so EVERY window can collide with it, not only the current month:
        // the tail of a closed month (27.07 to 31.07 of a July window) lies
        // inside the current week. Hence:
        //  - coverage reviews go only on posts CREATED BEFORE the current week,
        //    in every window;
        //  - in the current-month window coverage review DATES also stay before
        //    the week start and >=6h before `now`, so the freshest review on the
        //    site remains the organic one.
        var weekStart = WeekStartUtc(now);

        var nonDraftGameIds = await _dbContext.Set<DbGame>()
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync();

        var blogs = await _dbContext.Set<DbBlog>()
            .Where(b => !b.IsRemoved && b.Status != ModuleStatus.Draft)
            .ToListAsync();

        // Publication numbers are assigned across windows before a single
        // SaveChanges, so the DB max must be bumped in memory per blog.
        var nextPublicationNumber = new Dictionary<Guid, int>();

        // Each covered post belongs to exactly one window — review dates must
        // stay inside their window and a (post, reviewer) pair is unique.
        var usedPostIds = new HashSet<Guid>();

        async Task CoverWindow(DateTimeOffset winStart, DateTimeOffset winEnd, bool isCurrentMonth)
        {
            DateTimeOffset RandomReviewDate()
            {
                var cap = winEnd;
                if (isCurrentMonth)
                {
                    var reviewWindowEnd = weekStart < now.AddHours(-6) ? weekStart : now.AddHours(-6);
                    cap = reviewWindowEnd;
                }
                var minutes = (int)Math.Max(0, (cap - winStart).TotalMinutes);
                return winStart.AddMinutes(minutes == 0 ? 0 : _random.Next(0, minutes));
            }

            // ── Rating boards: TopPlayersByRating + TopGamesByRating ────────
            // Both sum PostReview.SignValue; measure the window exactly as the
            // boards do before topping up.
            var windowReviews = await _dbContext.PostReviews
                .Where(r => !r.IsRemoved && r.CreatedUtc >= winStart && r.CreatedUtc < winEnd)
                .Select(r => new { r.GameId, r.PostAuthorId, Sign = (int)r.SignValue })
                .ToListAsync();
            var positiveGames = windowReviews.GroupBy(r => r.GameId).Count(g => g.Sum(x => x.Sign) > 0);
            var positiveAuthors = windowReviews.GroupBy(r => r.PostAuthorId).Count(g => g.Sum(x => x.Sign) > 0);

            if (positiveGames < LeaderboardBoards.BoardSize || positiveAuthors < LeaderboardBoards.BoardSize)
            {
                // Every window stops at the week start: a topped-up post created
                // this week would outrank the showcase post in the weekly-best
                // widget, and a closed month's last days fall inside this week.
                // On top of that a closed window takes its own posts, so the
                // reviews sit next to the activity they praise.
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
                var candidates = (await candidatesQuery
                        .Select(po => new { po.PostId, po.AuthorId, GameId = po.Room.GameId })
                        .ToListAsync())
                    .Where(c => !usedPostIds.Contains(c.PostId))
                    .ToList();

                // One post per distinct (game, author) so one batch lights up
                // `target` distinct games AND `target` distinct authors.
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

                // Reviewers who already reviewed a candidate post must not be
                // reused for it — (post, reviewer) stays unique.
                var pickedIds = picked.Select(x => x.PostId).ToList();
                var existingPairs = (await _dbContext.PostReviews
                        .Where(r => pickedIds.Contains(r.PostId))
                        .Select(r => new { r.PostId, r.AuthorId })
                        .ToListAsync())
                    .Select(x => (x.PostId, x.AuthorId))
                    .ToHashSet();

                // Descending positive scores (target, target-1, …, 1) so the
                // boards read as a real ranking, not a wall of ties.
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
                        _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                        {
                            PostReviewId = _guidFactory.Create(),
                            AuthorId = reviewer.UserId,
                            PostId = post.PostId,
                            PostAuthorId = post.AuthorId,
                            GameId = post.GameId,
                            CreatedUtc = RandomReviewDate(),
                            Text = "Отличный отыгрыш!",
                            SignValue = (short)ReviewSign.Positive,
                            IsRemoved = false
                        });
                        result.ReviewsCreated++;
                    }
                }
            }

            // ── Blog boards: TopBlogsByRating (publication likes) +
            //    TopBlogsByPosts + TopBlogAuthorsByVolume. All three read
            //    publications dated in the window; give every blog without one
            //    a publication (with a few likes). ─────────────────────────
            var blogsWithWindowPub = (await _dbContext.Set<Publication>()
                .Where(pu => !pu.IsRemoved && pu.IsPublished
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

                const string content = "Свежая заметка этого месяца: делюсь наблюдениями, "
                    + "планами и парой историй с недавних игр. Впереди много интересного!";
                var windowCap = winEnd < now ? winEnd : now;
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
                    Preview = content[..Math.Min(100, content.Length)] + "...",
                    CreatedUtc = createdUtc,
                    IsPublished = true,
                    PublishedUtc = createdUtc,
                    CommentsEnabled = true,
                    ViewCount = _random.Next(10, 500),
                    CommentCount = 0,
                    IsRemoved = false
                };
                _dbContext.Set<Publication>().Add(publication);
                blog.PublicationCount++;
                result.PublicationsCreated++;

                // At least one like so TopBlogsByRating counts this blog too.
                var likers = users
                    .Where(u => u.UserId != blog.AuthorId)
                    .OrderBy(_ => _random.Next())
                    .Take(_random.Next(1, 6))
                    .ToList();
                foreach (var liker in likers)
                {
                    _dbContext.Set<Like>().Add(new Like
                    {
                        LikeId = _guidFactory.Create(),
                        EntityId = publication.PublicationId,
                        EntityType = LikeEntityType.Publication,
                        UserId = liker.UserId,
                        IsRemoved = false
                    });
                    result.LikesCreated++;
                }
            }
        }

        // Closed windows first so they claim their own posts; the current
        // month then covers itself with any remaining pre-week posts.
        await CoverWindow(prevMonthStart, monthStart, isCurrentMonth: false);
        await CoverWindow(prevYearDecStart, prevYearDecStart.AddMonths(1), isCurrentMonth: false);
        await CoverWindow(monthStart, monthStart.AddMonths(1), isCurrentMonth: true);

        await _dbContext.SaveChangesAsync();
        result.Details.Add("Ensured full top-10 rating coverage for the current month, the last closed month and the previous year");
    }

    private async Task UpdatePopularityScores(DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var activeThreshold = now - TimeSpan.FromDays(30);

        // Update game popularity scores (active players + readers)
        var gameIds = await _dbContext.Set<DbGame>()
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync();

        if (gameIds.Count > 0)
        {
            var playerCounts = await _dbContext.Set<Character>()
                .Where(c => gameIds.Contains(c.GameId) &&
                           c.Status == CharacterStatus.Active &&
                           !c.IsNpc &&
                           c.AuthorId.HasValue &&
                           c.Author != null &&
                           c.Author.LastActivityUtc.HasValue &&
                           c.Author.LastActivityUtc.Value > activeThreshold)
                .GroupBy(c => c.GameId)
                .Select(g => new { GameId = g.Key, Count = g.Select(c => c.AuthorId!.Value).Distinct().Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count);

            var gameReaderCounts = await _dbContext.Set<Subscription>()
                .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                           gameIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { GameId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count);

            var games = await _dbContext.Set<DbGame>().Where(g => gameIds.Contains(g.GameId)).ToListAsync();
            foreach (var game in games)
            {
                playerCounts.TryGetValue(game.GameId, out var playerCount);
                gameReaderCounts.TryGetValue(game.GameId, out var readerCount);
                game.PopularityScore = playerCount + readerCount;
                game.PopularityScoreUpdatedUtc = now;
            }
        }

        // Update blog popularity scores (active readers)
        var blogIds = await _dbContext.Set<DbBlog>()
            .Where(b => !b.IsRemoved)
            .Select(b => b.BlogId)
            .ToListAsync();

        if (blogIds.Count > 0)
        {
            var blogReaderCounts = await _dbContext.Set<Subscription>()
                .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                           blogIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { BlogId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BlogId, x => x.Count);

            var blogs = await _dbContext.Set<DbBlog>().Where(b => blogIds.Contains(b.BlogId)).ToListAsync();
            foreach (var blog in blogs)
            {
                blogReaderCounts.TryGetValue(blog.BlogId, out var readerCount);
                blog.PopularityScore = readerCount;
                blog.PopularityScoreUpdatedUtc = now;
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Updated popularity scores for {gameIds.Count} games and {blogIds.Count} blogs");
    }

    private async Task CreateLikes(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Check if likes already exist
        var existingLikesCount = await _dbContext.Set<Like>().CountAsync();
        if (existingLikesCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Likes already exist ({existingLikesCount}), skipping");
            return;
        }

        var likesCreated = 0;

        // Helper to add random likes to an entity
        void AddLikesForEntity(Guid entityId, LikeEntityType entityType, Guid? authorId)
        {
            // Each entity gets 0-5 likes from random users (excluding author)
            var likeCount = _random.Next(0, 6);
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
                    IsRemoved = false
                });
                likesCreated++;
            }
        }

        // 1. Likes for forum topics
        var topics = await _dbContext.Set<Topic>()
            .Where(t => !t.IsRemoved)
            .Select(t => new { t.TopicId, t.AuthorId })
            .ToListAsync();
        foreach (var topic in topics)
        {
            AddLikesForEntity(topic.TopicId, LikeEntityType.Topic, topic.AuthorId);
        }

        // 2. Likes for forum/publication comments
        var comments = await _dbContext.Set<DbComment>()
            .Where(c => !c.IsRemoved)
            .Select(c => new { c.CommentId, c.AuthorId })
            .ToListAsync();
        foreach (var comment in comments)
        {
            AddLikesForEntity(comment.CommentId, LikeEntityType.Comment, comment.AuthorId);
        }

        // 3. Likes for publications
        var publications = await _dbContext.Set<Publication>()
            .Where(p => !p.IsRemoved && p.IsPublished)
            .Select(p => new { p.PublicationId, p.AuthorId })
            .ToListAsync();
        foreach (var publication in publications)
        {
            AddLikesForEntity(publication.PublicationId, LikeEntityType.Publication, publication.AuthorId);
        }

        // 4. Likes for chat messages — both the global chat and personal/group DMs
        var messages = await _dbContext.Set<Message>()
            .Where(m => !m.IsRemoved)
            .Select(m => new { m.MessageId, m.UserId })
            .ToListAsync();
        foreach (var message in messages)
        {
            AddLikesForEntity(message.MessageId, LikeEntityType.Message, message.UserId);
        }

        // 5. Likes for post reviews (only entity that supports likes)
        var ratedPostReviews = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved)
            .Select(r => new { r.PostReviewId, r.AuthorId })
            .ToListAsync();
        foreach (var review in ratedPostReviews)
        {
            AddLikesForEntity(review.PostReviewId, LikeEntityType.PostReview, review.AuthorId);
        }

        await _dbContext.SaveChangesAsync();

        result.LikesCreated = likesCreated;
        result.Details.Add($"Created {likesCreated} likes");
    }
}
