using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
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
    /// SolohinLex (admin): backfill username change history and upload an avatar
    /// so the test admin profile has the same look-and-feel as a real veteran user.
    /// </summary>
    private async Task SetupSolohinLexProfile(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex not found, skipping profile setup");
            return;
        }

        // --- Username history ---
        // Always rebuild so re-seeds produce a deterministic history.
        var existingHistory = await _dbContext.UsernameHistories
            .Where(h => h.UserId == solohin.UserId)
            .ToListAsync();
        if (existingHistory.Count > 0)
        {
            _dbContext.UsernameHistories.RemoveRange(existingHistory);
        }

        // Three historical renames, oldest first: Lex (2020) → Solohin (2022) → AlexSolohin (2024) → SolohinLex (current)
        var historyEntries = new (string Old, string New, DateTimeOffset When)[]
        {
            ("Lex",          "Solohin",      now.AddYears(-4)),
            ("Solohin",      "AlexSolohin",  now.AddYears(-2)),
            ("AlexSolohin",  "SolohinLex",   now.AddMonths(-6)),
        };
        foreach (var (oldName, newName, when) in historyEntries)
        {
            _dbContext.UsernameHistories.Add(new DbUsernameHistory
            {
                UsernameHistoryId = _guidFactory.Create(),
                UserId = solohin.UserId,
                OldUsername = oldName,
                NewUsername = newName,
                ChangedUtc = when,
                ApprovedByUserId = solohin.UserId,
            });
        }

        // --- "О себе" (Info, BBCode) ---
        // Idempotent: always assign so re-seeds produce a known text.
        // The BBCode source lives in Assets/Seed/SolohinLex.bbcode and
        // is embedded into the assembly as an EmbeddedResource — the single copy
        // for the whole project (SSOT). It used to be duplicated as a raw string
        // here + in scripts/solohin-info.bbcode + scripts/update-solohin-info.sql;
        // both external copies are gone, the manual psql update is no longer needed.
        solohin.Info = await LoadEmbeddedTextAsync("DM.Tools.Seeder.Assets.Seed.SolohinLex.bbcode");

        // --- Contacts ---
        // Same idempotency rule as username history: wipe and rebuild so re-seeds
        // converge on a known state regardless of prior runs.
        var existingContacts = await _dbContext.Set<DbUserContact>()
            .Where(c => c.UserId == solohin.UserId)
            .ToListAsync();
        if (existingContacts.Count > 0)
        {
            _dbContext.Set<DbUserContact>().RemoveRange(existingContacts);
        }
        var contactEntries = new (string Type, string Value)[]
        {
            ("Telegram", "@solohin_lex"),
            ("Discord",  "solohinlex"),
        };
        for (var i = 0; i < contactEntries.Length; i++)
        {
            var (type, value) = contactEntries[i];
            _dbContext.Set<DbUserContact>().Add(new DbUserContact
            {
                UserContactId = _guidFactory.Create(),
                UserId = solohin.UserId,
                ContactType = type,
                ContactValue = value,
                SortOrder = i,
            });
        }

        // --- Endorsements ("Рекомендации") ---
        // Wipe-and-rebuild so re-seeds are deterministic. Endorsers are
        // looked up by username — if a username doesn't exist in this seed
        // run, that one entry is silently skipped.
        var existingEndorsements = await _dbContext.UserEndorsements
            .Where(e => e.TargetUserId == solohin.UserId)
            .ToListAsync();
        if (existingEndorsements.Count > 0)
        {
            _dbContext.UserEndorsements.RemoveRange(existingEndorsements);
        }
        // Plain text only (endorsements are not BBCode-rendered), about the
        // person as player / master / human — not about site administration.
        var endorsementEntries = new (string Username, int DaysAgo, string Text)[]
        {
            ("TestSeniorMod", 180, "Играем с Лексом уже несколько лет. Надежный согрок: пишет регулярно, не пропадает посреди сцены, спорные моменты за столом обсуждает спокойно. Рекомендую и как мастера, и как игрока."),
            ("TestModerator", 90,  "Отыгрывал у меня в двух кампаниях. Персонажей прописывает глубоко, в чужой отыгрыш не лезет, а его посты задают планку всей игре."),
            ("TestMentor",    60,  "Играл у него в Звездном Крейсере. Описания читаются как роман, NPC живые, сюжет не провисает. Если попадете на его набор — соглашайтесь не раздумывая."),
            ("Experienced",   30,  "Терпеливый к новичкам мастер: объяснит правила, поможет докрутить анкету и не бросит игру на середине. Таких поискать."),
            ("TestHonorary",  14,  "Знаком с ним еще со старой версии сайта. Как человек — открытый и отзывчивый, всегда готов подсказать по механике или помочь с идеей для персонажа."),
        };
        foreach (var (authorUsername, daysAgo, text) in endorsementEntries)
        {
            var author = users.FirstOrDefault(u => u.Username == authorUsername);
            if (author == null) continue;
            _dbContext.UserEndorsements.Add(new UserEndorsement
            {
                UserEndorsementId = _guidFactory.Create(),
                AuthorId = author.UserId,
                TargetUserId = solohin.UserId,
                CreatedUtc = now.AddDays(-daysAgo),
                Text = text,
                IsRemoved = false,
            });
        }

        // --- Awards ---
        // SolohinLex's demo contest history: tells a progression
        // 2022 → 2024 (from mid-table to the Grand Prix). Rules:
        //   - At most one placement per series (1/2/3 are mutually exclusive).
        //   - Special awards (Народное / Критик / Угадайка) are granted separately
        //     by jury/minigame decision and can be combined with a placement.
        //   - The grant date is tied to the series (season, year), not to now,
        //     so the sort order is natural.
        // Wipe-and-rebuild, like the other demo blocks.
        var existingAwards = await _dbContext.UserAwards
            .Where(a => a.UserId == solohin.UserId)
            .ToListAsync();
        if (existingAwards.Count > 0)
        {
            _dbContext.UserAwards.RemoveRange(existingAwards);
        }
        var lit23 = new Guid("00000000-0000-0000-0004-000000000001"); // 23rd literary contest, 2024
        var lit22 = new Guid("00000000-0000-0000-0004-000000000002"); // 22nd literary contest, 2023
        var lit20 = new Guid("00000000-0000-0000-0004-000000000004"); // 20th literary contest, 2022
        var art2 = new Guid("00000000-0000-0000-0004-000000000005"); // 2nd art contest, 2024
        // "1-й арт-конкурс 2023" (0004-...-06) intentionally holds no
        // SolohinLex award: his ONLY art award is the art2 place below.
        var contestFirst = new Guid("00000000-0000-0000-0001-000000000001");
        var contestSecond = new Guid("00000000-0000-0000-0001-000000000002");
        var contestThird = new Guid("00000000-0000-0000-0001-000000000003");
        var popularVote = new Guid("00000000-0000-0000-0001-000000000004");
        var bestCritic = new Guid("00000000-0000-0000-0001-000000000005");
        var guesser = new Guid("00000000-0000-0000-0001-000000000006");
        // SolohinLex chronology: lit career 2022-2024 + a single art award
        // (silver at art contest #2, 2024).
        // WorkUrl — a placeholder topic with the work itself (for placements + popular
        // vote). best_critic / guesser are not tied to a specific work.
        // Special-award bindings must make sense per contest type: best_critic
        // rewards REVIEWS (texts), so it only ever attaches to a literary
        // series; guesser (author guessing) and popular_vote (vote for a work)
        // fit any contest type.
        const string sampleWork = "https://dm.am/forum/topic/sample-work-";
        var demoAwards = new (Guid SeriesId, Guid AwardTypeId, DateTimeOffset At, string? WorkUrl)[]
        {
            (lit20, contestThird,  new DateTimeOffset(2022, 9,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit20-3"),
            (lit20, guesser,       new DateTimeOffset(2022, 9,  1, 12, 0, 0, TimeSpan.Zero), null),
            (lit22, contestSecond, new DateTimeOffset(2023, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit22-2"),
            (lit22, bestCritic,    new DateTimeOffset(2023, 3,  1, 12, 0, 0, TimeSpan.Zero), null),
            (lit23, contestFirst,  new DateTimeOffset(2024, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit23-1"),
            (lit23, popularVote,   new DateTimeOffset(2024, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit23-1"),
            (art2,  contestSecond, new DateTimeOffset(2024, 9,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "art2-2"),
        };
        foreach (var (seriesId, awardTypeId, at, workUrl) in demoAwards)
        {
            _dbContext.UserAwards.Add(new DM.Infrastructure.Persistence.Entities.Community.UserAward
            {
                UserAwardId = _guidFactory.Create(),
                UserId = solohin.UserId,
                AwardTypeId = awardTypeId,
                ContestSeriesId = seriesId,
                WorkUrl = workUrl,
                AwardedUtc = at,
                AwardedByUserId = solohin.UserId, // self-grant in the seeder; in prod — admin/seniormod
                IsRemoved = false,
            });
        }

        // Honorary goblin — a non-contest veteran honour dated to the user's
        // early years, so it is the OLDEST award and (awards sort oldest-first)
        // leads the list. SolohinLex is the site's "главный гоблин" (admin), so
        // he carries it too; the honour is not exclusive (TestHonorary keeps his
        // own copy). No ContestSeriesId → the tile shows the plain type.title/icon.
        var honoraryGoblinSince = new DateTimeOffset(2015, 6, 1, 12, 0, 0, TimeSpan.Zero);
        _dbContext.UserAwards.Add(new DM.Infrastructure.Persistence.Entities.Community.UserAward
        {
            UserAwardId = _guidFactory.Create(),
            UserId = solohin.UserId,
            AwardTypeId = new Guid("00000000-0000-0000-0001-000000000007"), // honorary_goblin
            ContestSeriesId = null,
            WorkUrl = null,
            AwardedUtc = honoraryGoblinSince,
            AwardedByUserId = solohin.UserId, // self-grant in the seeder; in prod — admin/seniormod
            IsRemoved = false,
        });
        result.Details.Add($"SolohinLex awards: rebuilt {demoAwards.Length} contest awards + honorary goblin across {demoAwards.Select(d => d.SeriesId).Distinct().Count()} contest series (2022-2024)");

        // --- TestHonorary award ---
        // Ex-honorary user: no longer a flag on the user entity, just holds
        // the "Почетный гоблин" award like anyone else can.
        var testHonorary = users.FirstOrDefault(u => u.Username == "TestHonorary");
        if (testHonorary != null)
        {
            var existingHonoraryAwards = await _dbContext.UserAwards
                .Where(a => a.UserId == testHonorary.UserId)
                .ToListAsync();
            if (existingHonoraryAwards.Count > 0)
            {
                _dbContext.UserAwards.RemoveRange(existingHonoraryAwards);
            }
            _dbContext.UserAwards.Add(new DM.Infrastructure.Persistence.Entities.Community.UserAward
            {
                UserAwardId = _guidFactory.Create(),
                UserId = testHonorary.UserId,
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000007"), // honorary_goblin
                ContestSeriesId = null, // non-contest award
                WorkUrl = null,
                AwardedUtc = honoraryGoblinSince, // "почетный гоблин с 2015 года" — matches the profile bio
                AwardedByUserId = solohin.UserId,
                IsRemoved = false,
            });
            result.Details.Add("TestHonorary awarded: Почетный гоблин");
        }

        // --- Avatar upload ---
        // Idempotent: skip if SolohinLex already has an avatar wired up.
        if (solohin.AvatarUploadId == null)
        {
            try
            {
                var assembly = typeof(DataSeeder).Assembly;
                const string resourceName = "DM.Tools.Seeder.Assets.Seed.SolohinLex.jpg";

                await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream != null)
                {
                    using var ms = new MemoryStream();
                    await resourceStream.CopyToAsync(ms);
                    var imageBytes = ms.ToArray();

                    var uploadId = _guidFactory.Create();
                    var upload = await SeedAvatarFromBytesAsync(
                        imageBytes,
                        declaredContentType: "image/jpeg",
                        sourceFileName: "SolohinLex.jpg",
                        type: UploadType.UserAvatar,
                        uploadId: uploadId,
                        userId: solohin.UserId,
                        entityId: solohin.UserId,
                        now: now);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(upload);
                    solohin.AvatarUploadId = uploadId;
                }
                else
                {
                    result.Details.Add("SolohinLex avatar resource not found (Assets/Seed/SolohinLex.jpg), skipping avatar");
                }
            }
            catch (Exception ex)
            {
                result.Details.Add($"SolohinLex avatar upload failed: {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"SolohinLex profile set up: {historyEntries.Length} username history entries, " +
            $"{contactEntries.Length} contacts, {endorsementEntries.Length} endorsements, " +
            $"info=yes, avatar={(solohin.AvatarUploadId != null ? "yes" : "no")}");
    }

    private async Task RecomputeUserRatings(ComprehensiveSeedResult result)
    {
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QualityRating" = COALESCE(s.sum_signs, 0)
            FROM (
                SELECT "Users"."UserId" AS user_id,
                       (SELECT COALESCE(SUM(r."SignValue"), 0)
                        FROM "PostReviews" r
                        JOIN "Posts" p ON p."PostId" = r."PostId"
                        WHERE r."IsRemoved" = false
                          AND p."IsRemoved" = false
                          AND p."AuthorId" = "Users"."UserId") AS sum_signs
                FROM "Users"
            ) s
            WHERE u."UserId" = s.user_id;
        """);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QuantityRating" = COALESCE(s.cnt, 0)
            FROM (
                SELECT "Users"."UserId" AS user_id,
                       (SELECT COUNT(*)
                        FROM "Posts" p
                        WHERE p."IsRemoved" = false
                          AND p."AuthorId" = "Users"."UserId") AS cnt
                FROM "Users"
            ) s
            WHERE u."UserId" = s.user_id;
        """);

        result.Details.Add("User ratings recomputed from actual PostReviews and Posts");
    }

    /// <summary>
    /// Boosts SolohinLex metrics so his achievements page shows all 5 tier
    /// plaque states, including platinum (T4) of the "Игровые посты" chain:
    /// bulk-inserts lightweight archive posts up to the T4 threshold (5000)
    /// into a room of a finished game he masters. The GamePostsAuthored
    /// metric reads the denormalized QuantityRating, which is then recounted
    /// from the real COUNT(Posts) — data and counter stay consistent.
    /// Idempotent: marker is the GameText prefix, only the missing amount is
    /// topped up. Must run AFTER <see cref="RecomputeUserRatings"/>, which
    /// would otherwise overwrite the QualityRating boost.
    /// </summary>
    private async Task BoostSolohinLexMetrics(DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Game posts tier ladder: T1=100, T2=500, T3=2000, T4=5000.
        const int targetArchivePosts = 5000;
        const string archiveMarker = "Архивная запись №";

        var solohin = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex metrics boost skipped: user not found");
            return;
        }

        var existing = await _dbContext.Posts
            .CountAsync(p => p.AuthorId == solohin.UserId
                && !p.IsRemoved
                && p.GameText.StartsWith(archiveMarker));
        var toCreate = targetArchivePosts - existing;
        if (toCreate > 0)
        {
            // A finished game mastered by SolohinLex is a plausible home for
            // thousands of archive posts (a long completed campaign).
            var game = await _dbContext.Set<DbGame>()
                .Where(g => g.MasterId == solohin.UserId
                    && !g.IsRemoved
                    && g.Status == ModuleStatus.Closed
                    && g.ClosedReason == ClosedReason.Finished)
                .OrderBy(g => g.CreatedUtc)
                .FirstOrDefaultAsync();
            var room = game == null
                ? null
                : await _dbContext.Rooms
                    .Where(r => r.GameId == game.GameId && !r.IsRemoved && r.Type == RoomType.Default)
                    .OrderBy(r => r.RoomNumber)
                    .FirstOrDefaultAsync();

            if (room == null)
            {
                result.Details.Add("SolohinLex archive posts skipped: no finished mastered game with a room");
            }
            else
            {
                // Master posts (no character), short text, spread evenly
                // between game activation and closing — always in the past,
                // so they never surface in "recent activity" widgets.
                var windowStart = game!.ActivatedUtc ?? game.CreatedUtc;
                var windowEnd = game.ClosedUtc ?? now.AddDays(-1);
                if (windowEnd <= windowStart) windowEnd = windowStart.AddDays(30);
                var stepTicks = (windowEnd - windowStart).Ticks / targetArchivePosts;

                var posts = new List<Post>(toCreate);
                for (var i = 0; i < toCreate; i++)
                {
                    var ordinal = existing + i + 1;
                    posts.Add(new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = room.RoomId,
                        CharacterId = null,
                        AuthorId = solohin.UserId,
                        CreatedUtc = windowStart.AddTicks(stepTicks * ordinal),
                        GameText = $"{archiveMarker}{ordinal}. Летопись кампании, сохранена для истории.",
                        IsRemoved = false,
                    });
                }
                _dbContext.Posts.AddRange(posts);
                await _dbContext.SaveChangesAsync();
                result.PostsCreated += toCreate;
            }
        }

        // QuantityRating = real post count (archive + organic game posts) →
        // T4 "Приключение в жизнь" (5000). QualityRating: 350 → silver
        // (T1=100, T2=250 earned; T3=500, T4=1000 — not).
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QuantityRating" = (SELECT COUNT(*) FROM "Posts" p
                                    WHERE p."AuthorId" = u."UserId" AND p."IsRemoved" = false),
                "QualityRating" = 350
            WHERE u."Username" = 'SolohinLex';
        """);

        sw.Stop();
        result.Details.Add(
            $"SolohinLex metrics boosted: +{Math.Max(toCreate, 0)} archive game posts " +
            $"(target {targetArchivePosts} → posts T4 platinum), rating=350 (silver), took {sw.ElapsedMilliseconds} ms");
    }
}
