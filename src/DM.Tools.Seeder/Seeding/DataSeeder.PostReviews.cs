using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The finished games the organic review pass rates, their open rooms and
    /// the posts in them.
    /// </summary>
    private sealed record FinishedGamePosts(
        List<Guid> GameIds, List<Room> Rooms, Dictionary<Guid, Guid> RoomToGameId, List<Post> Posts);

    /// <summary>
    /// The posts of the finished games, from the change tracker of this run, or
    /// from the database on a re-seed.
    /// </summary>
    /// <returns>Null when the seed holds no finished game.</returns>
    private async Task<FinishedGamePosts?> ReadFinishedGamePosts()
    {
        var finishedGameIds = _dbContext.ChangeTracker.Entries<DbGame>()
            .Where(e => e.Entity.Status == ModuleStatus.Closed && e.Entity.ClosedReason == ClosedReason.Finished)
            .Select(e => e.Entity.GameId)
            .ToList();

        if (finishedGameIds.Count == 0)
        {
            finishedGameIds = await _dbContext.Set<DbGame>()
                .Where(g => g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished)
                .Select(g => g.GameId)
                .ToListAsync();
        }

        if (finishedGameIds.Count == 0)
        {
            return null;
        }

        var roomsFromTracker = _dbContext.ChangeTracker.Entries<Room>()
            .Where(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.AccessType == RoomAccessType.Open)
            .Select(e => e.Entity)
            .ToList();

        List<Room> rooms;
        if (roomsFromTracker.Count > 0)
        {
            rooms = roomsFromTracker;
        }
        else
        {
            rooms = await _dbContext.Set<Room>()
                .Where(r => finishedGameIds.Contains(r.GameId) && r.AccessType == RoomAccessType.Open)
                .ToListAsync();
        }

        var roomIds = rooms.Select(r => r.RoomId).ToList();
        var roomToGameId = rooms.ToDictionary(r => r.RoomId, r => r.GameId);

        var postsFromTracker = _dbContext.ChangeTracker.Entries<Post>()
            .Where(e => !e.Entity.IsRemoved && roomIds.Contains(e.Entity.RoomId))
            .Select(e => e.Entity)
            .ToList();

        List<Post> posts;
        if (postsFromTracker.Count > 0)
        {
            posts = postsFromTracker;
        }
        else
        {
            posts = await _dbContext.Set<Post>()
                .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
                .ToListAsync();
        }

        return new FinishedGamePosts(finishedGameIds, rooms, roomToGameId, posts);
    }

    /// <summary>
    /// Random reviews of the finished games' posts the showcase left alone:
    /// thirty posts of this week with two to four reviews each, then eight older
    /// ones with a single review.
    /// </summary>
    private void SeedRandomPostReviews(
        FinishedGamePosts finished, HashSet<Guid> processedPostIds, List<DbUser> experiencedUsers,
        ComprehensiveSeedResult result)
    {
        var (_, _, roomToGameId, posts) = finished;

        // Enough for two pages at 20 per page
        var thisWeekPosts = posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(30).ToList();
        foreach (var post in thisWeekPosts)
        {
            processedPostIds.Add(post.PostId);
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            var reviewCount = _random.Next(2, 5);
            var availableReviewers = experiencedUsers.Where(u => u.UserId != post.AuthorId).ToList();

            for (var r = 0; r < reviewCount && r < availableReviewers.Count; r++)
            {
                var reviewer = availableReviewers[r];
                var sign = (ReviewSign)_random.Next(-1, 2);
                var reviewDate = _now.AddDays(-_random.Next(0, 6)).AddHours(-_random.Next(1, 24));
                AddRandomPostReview(post, gameId, reviewer, sign, reviewDate, result);
            }
        }

        foreach (var post in posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(8))
        {
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            var reviewer = experiencedUsers.FirstOrDefault(u => u.UserId != post.AuthorId);
            if (reviewer != null)
            {
                var sign = (ReviewSign)_random.Next(-1, 2);
                var reviewDate = _now.AddDays(-_random.Next(14, 28)).AddHours(-_random.Next(1, 24));
                AddRandomPostReview(post, gameId, reviewer, sign, reviewDate, result);
            }
        }
    }

    /// <summary>A review of a post, counted.</summary>
    private void AddPostReview(
        Guid postId, Guid postAuthorId, Guid gameId, Guid reviewerId, DateTimeOffset createdUtc, string text,
        ReviewSign sign, ComprehensiveSeedResult result)
    {
        _dbContext.PostReviews.Add(new DbPostReview
        {
            PostReviewId = _guidFactory.Create(),
            AuthorId = reviewerId,
            PostId = postId,
            PostAuthorId = postAuthorId,
            GameId = gameId,
            CreatedUtc = createdUtc,
            Text = text,
            SignValue = (short)sign,
        });
        result.ReviewsCreated++;
    }

    // The wording is drawn here and the sign and the date by the caller: SeedDeterminism pins the order of draws.
    private void AddRandomPostReview(
        Post reviewed, Guid reviewedGameId, DbUser reviewer, ReviewSign sign, DateTimeOffset reviewDate,
        ComprehensiveSeedResult result)
    {
        var text = ReviewText(sign);

        AddPostReview(reviewed.PostId, reviewed.AuthorId, reviewedGameId, reviewer.UserId,
            reviewDate, text, sign, result);
    }

    /// <summary>
    /// Spreads post reviews across the seeded games, so the ratings pulse and the per-game review pages
    /// have history: a few rated posts in most games, positive dominant, dated over months.
    /// </summary>
    /// <remarks>
    /// Every review keeps the write rules of PostReviewService. Runs before EnsureLeaderboardCoverage, which
    /// then tops up only what is missing; everything stays before the current week, for the homepage widgets.
    /// </remarks>
    private async Task SpreadPostReviews(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // A review older than the passes before this one write marks a previous run, and a reseed does nothing.
        var alreadySpread = await _dbContext.PostReviews
            .AnyAsync(r => r.CreatedUtc < _now.AddDays(-60));
        if (alreadySpread)
        {
            result.Details.Add("Post reviews already spread across games, skipping");
            return;
        }

        var weekStart = WeekStartUtc(_now);

        var candidates = await SpreadCandidates(weekStart);
        var rules = await ReadReviewerRules();

        // Newbies rate neutral only, by the counter the API reads.
        var experienced = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        var newbies = users.Where(u => ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        if (experienced.Count == 0)
        {
            result.Details.Add("No experienced users for the post review spread, skipping");
            return;
        }

        // Enough for the pulse and the per-game pages, small enough for a fast reseed.
        const int maxReviews = 350;
        var created = 0;
        var gamesTouched = new HashSet<Guid>();

        foreach (var gameGroup in candidates.GroupBy(c => c.GameId))
        {
            if (created >= maxReviews) break;

            var spread = SpreadGameReviews(gameGroup, maxReviews - created, experienced, newbies, rules, weekStart, result);
            created += spread;
            if (spread > 0)
            {
                gamesTouched.Add(gameGroup.Key);
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Spread {created} post reviews across {gamesTouched.Count} games");
    }

    /// <summary>A post the spread may rate, with the game it is in.</summary>
    private sealed record SpreadCandidate(Guid PostId, Guid AuthorId, DateTimeOffset CreatedUtc, Guid GameId);

    /// <summary>
    /// Posts a reviewer with no special access could legally rate, through the gate PostReviewService
    /// reads (GameAccessibilityFilters), ordered by creation and id.
    /// </summary>
    private async Task<List<SpreadCandidate>> SpreadCandidates(DateTimeOffset weekStart)
    {
        var rows = await _dbContext.Posts
            .Where(p => !p.IsRemoved
                && !p.Room.IsRemoved
                && p.Room.AccessType == RoomAccessType.Open
                && !p.Room.Game.IsRemoved
                && p.Room.Game.Status != ModuleStatus.Draft
                && p.Room.Game.PremoderationStatus == PremoderationStatus.Approved
                && p.CreatedUtc < weekStart)
            .OrderBy(p => p.CreatedUtc)
            .ThenBy(p => p.PostId)
            .Select(p => new { p.PostId, p.AuthorId, p.CreatedUtc, GameId = p.Room.GameId })
            .ToListAsync();

        return rows.Select(p => new SpreadCandidate(p.PostId, p.AuthorId, p.CreatedUtc, p.GameId)).ToList();
    }

    /// <summary>
    /// The write rules of PostReviewService a reviewer of the spread is held to,
    /// besides the sign a newbie may give (<see cref="SpreadSign"/>): one review
    /// per (post, reviewer) pair, and one reviewer's reviews in the same game
    /// over three days apart, in both directions.
    /// </summary>
    private sealed class ReviewerRules(
        HashSet<(Guid PostId, Guid AuthorId)> existingPairs,
        Dictionary<(Guid AuthorId, Guid GameId), List<DateTimeOffset>> reviewMoments)
    {
        public bool HasReviewed(Guid postId, Guid reviewerId) => existingPairs.Contains((postId, reviewerId));

        public bool CooldownClear(Guid reviewerId, Guid gameId, DateTimeOffset moment) =>
            !reviewMoments.TryGetValue((reviewerId, gameId), out var moments)
            || moments.All(m => Math.Abs((moment - m).TotalDays) > 3);

        public void Remember(Guid postId, Guid reviewerId, Guid gameId, DateTimeOffset moment)
        {
            existingPairs.Add((postId, reviewerId));
            if (!reviewMoments.TryGetValue((reviewerId, gameId), out var moments))
            {
                moments = new List<DateTimeOffset>();
                reviewMoments[(reviewerId, gameId)] = moments;
            }
            moments.Add(moment);
        }
    }

    /// <summary>
    /// The reviews already stored, as the rules of the spread read them.
    /// </summary>
    private async Task<ReviewerRules> ReadReviewerRules()
    {
        // The (post, reviewer) pairs already written: the unique index allows each once.
        var existingPairs = (await _dbContext.PostReviews
                .Select(r => new { r.PostId, r.AuthorId })
                .ToListAsync())
            .Select(x => (x.PostId, x.AuthorId))
            .ToHashSet();

        var reviewMoments = (await _dbContext.PostReviews
                .Where(r => !r.IsRemoved)
                .Select(r => new { r.AuthorId, r.GameId, r.CreatedUtc })
                .ToListAsync())
            .GroupBy(x => (x.AuthorId, x.GameId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.CreatedUtc).ToList());

        return new ReviewerRules(existingPairs, reviewMoments);
    }

    /// <summary>
    /// The sign of a spread review: neutral from a newbie, the only sign one may
    /// give, and otherwise positive dominant, some negative: roughly how a site
    /// where people rate what they liked distributes.
    /// </summary>
    private ReviewSign SpreadSign(DbUser reviewer)
    {
        if (ProbationPolicy.IsNewbie(reviewer.QuantityRating))
        {
            return ReviewSign.Neutral;
        }

        var roll = _random.Next(100);
        return roll < 62 ? ReviewSign.Positive
            : roll < 85 ? ReviewSign.Neutral
            : ReviewSign.Negative;
    }

    private static readonly string[] PositiveReviewTexts =
    [
        "Хороший пост, прочитал с удовольствием.",
        "Сцена движется, ждать следующего хода не скучно.",
        "Персонаж говорит своим голосом, верю.",
        "[b]Браво![/b] Одна из лучших сцен в игре.",
        "В духе сеттинга, детали на месте.",
        "Хороший слог, читается легко.",
        "Ответил всем, кто его звал, и сцену не потерял.",
        "Окружение прописано подробно, видно комнату.",
        "Хороший ход, такого поворота не ждал.",
        "[quote]Тихо, кто-то идет[/quote]\nЭта строчка вытянула всю сцену.",
        "Разбор по пунктам:\n[spoiler]Темп выдержан, мотивация персонажа ясна, крючок для следующей сцены оставлен. Придраться не к чему.[/spoiler]",
    ];

    private static readonly string[] NeutralReviewTexts =
    [
        "Ровный пост, без взлетов и провалов.",
        "Нормально, но развязка предсказуемая.",
        "Читабельно. Жду, куда повернет сцена.",
        "Есть удачные моменты, есть проходные.",
        "Аккуратно, по правилам, без искры.",
        "Нормально",
    ];

    private static readonly string[] NegativeReviewTexts =
    [
        "Слишком коротко для такой важной сцены.",
        "Персонаж действует вразрез с собственной анкетой.",
        "Много воды, действия почти нет.",
        "[spoiler]Придирка: реплики звучат одинаково у всех персонажей.[/spoiler]",
        "Можно лучше",
    ];

    /// <summary>The one set of review texts, some with BBCode, for every seeded review of a post.</summary>
    private string ReviewText(ReviewSign sign) => sign switch
    {
        ReviewSign.Positive => PositiveReviewTexts[_random.Next(PositiveReviewTexts.Length)],
        ReviewSign.Negative => NegativeReviewTexts[_random.Next(NegativeReviewTexts.Length)],
        _ => NeutralReviewTexts[_random.Next(NeutralReviewTexts.Length)]
    };

    /// <summary>
    /// The reviews of one game: two to four rated posts at even strides over its
    /// timeline, each with one to three reviewers.
    /// </summary>
    /// <returns>How many reviews it wrote, never more than the budget.</returns>
    private int SpreadGameReviews(
        IGrouping<Guid, SpreadCandidate> gameGroup, int budget, List<DbUser> experienced, List<DbUser> newbies,
        ReviewerRules rules, DateTimeOffset weekStart, ComprehensiveSeedResult result)
    {
        var gamePosts = gameGroup.ToList();
        if (gamePosts.Count < 2) return 0;

        var created = 0;

        // 2-4 rated posts per game, picked at even strides over the
        // post list so they cover the game's whole timeline.
        var ratedPostTarget = Math.Min(gamePosts.Count, _random.Next(2, 5));
        var stride = (double)gamePosts.Count / ratedPostTarget;
        for (var i = 0; i < ratedPostTarget && created < budget; i++)
        {
            var post = gamePosts[(int)(i * stride)];

            // A review lands strictly after its post and before the
            // current week; a post without at least a day of room for
            // that is too fresh to rate here.
            var windowStart = post.CreatedUtc.AddHours(1);
            var windowMinutes = (int)(weekStart - windowStart).TotalMinutes;
            if (windowMinutes < 24 * 60) continue;
            var baseMoment = windowStart.AddMinutes(_random.Next(0, windowMinutes));

            var reviewsWanted = _random.Next(1, 4);

            // Experienced reviewers in a fresh deterministic order per
            // post; every fifth rated post leads with a newbie, whose
            // review the sign rule below forces to neutral.
            var pool = experienced.OrderBy(_ => _random.Next()).ToList();
            if (newbies.Count > 0 && _random.Next(5) == 0)
            {
                pool.Insert(0, newbies[_random.Next(newbies.Count)]);
            }

            var added = 0;
            foreach (var reviewer in pool)
            {
                if (added >= reviewsWanted || created >= budget) break;
                if (reviewer.UserId == post.AuthorId) continue;
                if (rules.HasReviewed(post.PostId, reviewer.UserId)) continue;

                // Reviews of one post trail its first review by up to
                // three days, and all of them stay before the week start.
                var trailCapMinutes = (int)Math.Min(
                    (weekStart - baseMoment).TotalMinutes - 1,
                    TimeSpan.FromDays(3).TotalMinutes);
                var moment = trailCapMinutes <= 0
                    ? baseMoment
                    : baseMoment.AddMinutes(_random.Next(0, trailCapMinutes));
                if (!rules.CooldownClear(reviewer.UserId, gameGroup.Key, moment)) continue;

                var sign = SpreadSign(reviewer);

                AddPostReview(post.PostId, post.AuthorId, gameGroup.Key, reviewer.UserId,
                    moment, ReviewText(sign), sign, result);
                rules.Remember(post.PostId, reviewer.UserId, gameGroup.Key, moment);
                created++;
                added++;
            }
        }

        return created;
    }
}
