using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Spreads post reviews across the seeded games so the ratings pulse and
    /// the per-game review pages read as a living site rather than a
    /// single-game one: most games with posts get a few rated posts, from
    /// varied reviewers, with a positive-dominant sign mix and dates spread
    /// over the months of history the game pass laid down.
    ///
    /// Every review honors the write rules of PostReviewService, so this data
    /// could have been produced through the API: the reviewer is never the
    /// post author, one review per (post, reviewer) pair, posts sit in open
    /// rooms of approved non-draft games, newbies rate neutral only, and one
    /// reviewer's reviews in the same game stay over the three-day cooldown
    /// apart. QualityRating is not touched here: RecomputeUserRatings derives
    /// it from the stored reviews later in the run.
    ///
    /// Runs BEFORE EnsureLeaderboardCoverage and saves, so coverage measures
    /// its windows over these rows and tops up only what is still missing,
    /// and its (post, reviewer) guard sees them in the database. Both the
    /// rated posts and the review dates stay before the current week, so the
    /// homepage weekly widgets keep showing the organic showcase posts.
    /// </summary>
    private async Task SpreadPostReviews(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // The passes that ran before this one date reviews at most a month
        // back, while this spread reaches months into the game history, so
        // "a review much older than that exists" identifies a previous run
        // of the spread (or of the coverage pass that follows it) and makes
        // a re-seed over a populated database a no-op.
        var alreadySpread = await _dbContext.PostReviews
            .AnyAsync(r => r.CreatedUtc < now.AddDays(-60));
        if (alreadySpread)
        {
            result.Skipped++;
            result.Details.Add("Post reviews already spread across games, skipping");
            return;
        }

        var weekStart = WeekStartUtc(now);

        // Posts a reviewer with no special access could legally rate: an open
        // room of an approved, non-draft, non-removed game — the same gate
        // PostReviewService reads through GameAccessibilityFilters. Ordered by
        // creation and id so the game grouping below is part of the fixture,
        // not of the query plan.
        var candidates = await _dbContext.Posts
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

        // (post, reviewer) pairs already written by the organic review pass —
        // the unique index allows each pair once.
        var existingPairs = (await _dbContext.PostReviews
                .Select(r => new { r.PostId, r.AuthorId })
                .ToListAsync())
            .Select(x => (x.PostId, x.AuthorId))
            .ToHashSet();

        // The API refuses a second review in the same game within three days
        // of the previous one, so every same-game pair of one reviewer's
        // dates must sit over three days apart — in both directions, because
        // the reviews would have been created in chronological order and each
        // creation looks three days back.
        var reviewMoments = (await _dbContext.PostReviews
                .Where(r => !r.IsRemoved)
                .Select(r => new { r.AuthorId, r.GameId, r.CreatedUtc })
                .ToListAsync())
            .GroupBy(x => (x.AuthorId, x.GameId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.CreatedUtc).ToList());

        bool CooldownClear(Guid reviewerId, Guid gameId, DateTimeOffset moment) =>
            !reviewMoments.TryGetValue((reviewerId, gameId), out var moments)
            || moments.All(m => Math.Abs((moment - m).TotalDays) > 3);

        void RememberMoment(Guid reviewerId, Guid gameId, DateTimeOffset moment)
        {
            if (!reviewMoments.TryGetValue((reviewerId, gameId), out var moments))
            {
                moments = new List<DateTimeOffset>();
                reviewMoments[(reviewerId, gameId)] = moments;
            }
            moments.Add(moment);
        }

        // Newbies may only rate neutral, and the newbie badge is read from the
        // same counter the API reads at write time.
        var experienced = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        var newbies = users.Where(u => ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        if (experienced.Count == 0)
        {
            result.Details.Add("No experienced users for the post review spread, skipping");
            return;
        }

        // Positive dominant, some negative: roughly how a site where people
        // rate what they liked distributes.
        ReviewSign DrawSign()
        {
            var roll = _random.Next(100);
            return roll < 62 ? ReviewSign.Positive
                : roll < 85 ? ReviewSign.Neutral
                : ReviewSign.Negative;
        }

        var positiveTexts = new[]
        {
            "Сильный пост, прочитал с удовольствием.",
            "Отличная динамика сцены, так держать!",
            "Живой персонаж, веришь каждому слову.",
            "[b]Браво![/b] Одна из лучших сцен в игре.",
            "Атмосферно и очень в духе сеттинга.",
            "Хороший слог, читается на одном дыхании.",
            "Классное взаимодействие с партией, люблю такие посты.",
            "Детали окружения прописаны здорово.",
            "Отличный ход! Не ожидал такого поворота.",
            "Разбор по пунктам:\n[spoiler]Темп выдержан, мотивация персонажа ясна, крючок для следующей сцены оставлен. Придраться не к чему.[/spoiler]",
        };
        var neutralTexts = new[]
        {
            "Ровный пост, без взлетов и провалов.",
            "Нормально, но развязка предсказуемая.",
            "Читабельно. Жду, куда повернет сцена.",
            "Есть удачные моменты, есть проходные.",
            "Аккуратно, по правилам, без искры.",
        };
        var negativeTexts = new[]
        {
            "Слишком коротко для такой важной сцены.",
            "Персонаж действует вразрез с собственной анкетой.",
            "Много воды, действия почти нет.",
            "[spoiler]Придирка: реплики звучат одинаково у всех персонажей.[/spoiler]",
        };

        string DrawText(ReviewSign sign) => sign switch
        {
            ReviewSign.Positive => positiveTexts[_random.Next(positiveTexts.Length)],
            ReviewSign.Negative => negativeTexts[_random.Next(negativeTexts.Length)],
            _ => neutralTexts[_random.Next(neutralTexts.Length)]
        };

        // A dev stand, not a load test: enough for the pulse and the per-game
        // pages to fill up, small enough for a re-seed to stay fast.
        const int maxReviews = 350;
        var created = 0;
        var gamesTouched = new HashSet<Guid>();

        foreach (var gameGroup in candidates.GroupBy(c => c.GameId))
        {
            if (created >= maxReviews) break;

            var gamePosts = gameGroup.ToList();
            if (gamePosts.Count < 2) continue;

            // 2-4 rated posts per game, picked at even strides over the
            // post list so they cover the game's whole timeline.
            var ratedPostTarget = Math.Min(gamePosts.Count, _random.Next(2, 5));
            var stride = (double)gamePosts.Count / ratedPostTarget;
            for (var i = 0; i < ratedPostTarget && created < maxReviews; i++)
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
                    if (added >= reviewsWanted || created >= maxReviews) break;
                    if (reviewer.UserId == post.AuthorId) continue;
                    if (existingPairs.Contains((post.PostId, reviewer.UserId))) continue;

                    // Reviews of one post trail its first review by up to
                    // three days, and all of them stay before the week start.
                    var trailCapMinutes = (int)Math.Min(
                        (weekStart - baseMoment).TotalMinutes - 1,
                        TimeSpan.FromDays(3).TotalMinutes);
                    var moment = trailCapMinutes <= 0
                        ? baseMoment
                        : baseMoment.AddMinutes(_random.Next(0, trailCapMinutes));
                    if (!CooldownClear(reviewer.UserId, gameGroup.Key, moment)) continue;

                    var sign = ProbationPolicy.IsNewbie(reviewer.QuantityRating)
                        ? ReviewSign.Neutral
                        : DrawSign();

                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = post.PostId,
                        PostAuthorId = post.AuthorId,
                        GameId = gameGroup.Key,
                        CreatedUtc = moment,
                        Text = DrawText(sign),
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    existingPairs.Add((post.PostId, reviewer.UserId));
                    RememberMoment(reviewer.UserId, gameGroup.Key, moment);
                    result.ReviewsCreated++;
                    created++;
                    added++;
                    gamesTouched.Add(gameGroup.Key);
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Spread {created} post reviews across {gamesTouched.Count} games");
    }
}
