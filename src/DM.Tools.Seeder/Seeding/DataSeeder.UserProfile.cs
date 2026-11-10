using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Community;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private async Task SetupSolohinLexProfile(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        if (solohin == null)
        {
            result.Details.Add("SolohinLex not found, skipping profile setup");
            return;
        }

        var historyCount = await SeedSolohinLexUsernameHistory(solohin);

        await SeedSolohinLexAvatar(solohin);

        // "О себе" from Assets/Seed/SolohinLex.bbcode, assigned on every run: a
        // sample of the tags around pictures, each one the avatar above, so the
        // stand serves every picture of the page itself.
        var avatar = await _dbContext.Uploads.FindAsync(solohin.AvatarUploadId);
        solohin.Info = (await LoadEmbeddedTextAsync(SolohinLexInfo))
            .Replace("{avatar}", avatar?.FilePath, StringComparison.Ordinal);

        var contactCount = await SeedSolohinLexContacts(solohin);
        var endorsementCount = await SeedSolohinLexEndorsements(users, solohin);
        await SeedSolohinLexAwards(solohin, result);
        await SeedHonoraryGoblins(users, solohin, result);

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"SolohinLex profile set up: {historyCount} username history entries, " +
            $"{contactCount} contacts, {endorsementCount} endorsements, " +
            $"info=yes, avatar={(solohin.AvatarUploadId != null ? "yes" : "no")}");
    }

    private async Task<int> SeedSolohinLexUsernameHistory(DbUser solohin)
    {
        _dbContext.UsernameHistories.RemoveRange(
            await _dbContext.UsernameHistories.Where(h => h.UserId == solohin.UserId).ToListAsync());

        // Oldest first: Lex (2020), Solohin (2022), AlexSolohin (2024), SolohinLex now
        var historyEntries = new (string Old, string New, DateTimeOffset When)[]
        {
            ("Lex",          "Solohin",      _now.AddYears(-4)),
            ("Solohin",      "AlexSolohin",  _now.AddYears(-2)),
            ("AlexSolohin",  PrimaryLogin,   _now.AddMonths(-6)),
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

        return historyEntries.Length;
    }

    private async Task<int> SeedSolohinLexContacts(DbUser solohin)
    {
        _dbContext.Set<DbUserContact>().RemoveRange(
            await _dbContext.Set<DbUserContact>().Where(c => c.UserId == solohin.UserId).ToListAsync());
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

        return contactEntries.Length;
    }

    private async Task<int> SeedSolohinLexEndorsements(List<DbUser> users, DbUser solohin)
    {
        // An endorser missing from this run is skipped.
        _dbContext.UserEndorsements.RemoveRange(
            await _dbContext.UserEndorsements.Where(e => e.TargetUserId == solohin.UserId).ToListAsync());
        // Plain text, not BBCode, about the person and not about site administration.
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
                CreatedUtc = _now.AddDays(-daysAgo),
                Text = text,
            });
        }

        return endorsementEntries.Length;
    }

    private async Task SeedSolohinLexAwards(DbUser solohin, ComprehensiveSeedResult result)
    {
        // From mid-table in 2022 to the Grand Prix in 2024: one placement per series, the special awards
        // beside a placement, the grant date tied to the series.
        _dbContext.UserAwards.RemoveRange(
            await _dbContext.UserAwards.Where(a => a.UserId == solohin.UserId).ToListAsync());
        var lit23 = new Guid("00000000-0000-0000-0004-000000000001"); // 23rd literary contest, summer 2026
        var lit22 = new Guid("00000000-0000-0000-0004-000000000002"); // 22nd literary contest, winter 2026
        var lit20 = new Guid("00000000-0000-0000-0004-000000000004"); // 20th literary contest, winter 2025
        var art2 = new Guid("00000000-0000-0000-0004-000000000005"); // 2nd art contest, 2016
        // No SolohinLex award in "1-й арт-конкурс 2013": his only art award is the art2 place below.
        var contestFirst = new Guid("00000000-0000-0000-0001-000000000001");
        var contestSecond = new Guid("00000000-0000-0000-0001-000000000002");
        var contestThird = new Guid("00000000-0000-0000-0001-000000000003");
        var popularVote = new Guid("00000000-0000-0000-0001-000000000004");
        var bestCritic = new Guid("00000000-0000-0000-0001-000000000005");
        var guesser = new Guid("00000000-0000-0000-0001-000000000006");
        // WorkUrl is a placeholder topic with the work. best_critic rewards reviews, so it attaches only to a
        // literary series; guesser and popular_vote fit any contest.
        const string sampleWork = "/forum/topic/sample-work-";
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
            _dbContext.UserAwards.Add(new UserAward
            {
                UserAwardId = _guidFactory.Create(),
                UserId = solohin.UserId,
                AwardTypeId = awardTypeId,
                ContestSeriesId = seriesId,
                WorkUrl = workUrl,
                AwardedUtc = at,
                AwardedByUserId = solohin.UserId, // self-grant in the seeder; in prod — admin/seniormod
            });
        }
        result.Details.Add($"SolohinLex awards: rebuilt {demoAwards.Length} contest awards + honorary goblin across {demoAwards.Select(d => d.SeriesId).Distinct().Count()} contest series (2022-2024)");
    }

    private async Task SeedHonoraryGoblins(List<DbUser> users, DbUser solohin, ComprehensiveSeedResult result)
    {
        // Dated to his early years, so the oldest award leads the list. Without ContestSeriesId the tile shows the type.
        var honoraryGoblinSince = new DateTimeOffset(2015, 6, 1, 12, 0, 0, TimeSpan.Zero);
        _dbContext.UserAwards.Add(new UserAward
        {
            UserAwardId = _guidFactory.Create(),
            UserId = solohin.UserId,
            AwardTypeId = new Guid("00000000-0000-0000-0001-000000000007"), // honorary_goblin
            ContestSeriesId = null,
            WorkUrl = null,
            AwardedUtc = honoraryGoblinSince,
            AwardedByUserId = solohin.UserId, // self-grant in the seeder; in prod — admin/seniormod
        });

        var testHonorary = users.FirstOrDefault(u => u.Username == "TestHonorary");
        if (testHonorary != null)
        {
            _dbContext.UserAwards.RemoveRange(
                await _dbContext.UserAwards.Where(a => a.UserId == testHonorary.UserId).ToListAsync());
            _dbContext.UserAwards.Add(new UserAward
            {
                UserAwardId = _guidFactory.Create(),
                UserId = testHonorary.UserId,
                AwardTypeId = new Guid("00000000-0000-0000-0001-000000000007"), // honorary_goblin
                ContestSeriesId = null, // non-contest award
                WorkUrl = null,
                AwardedUtc = honoraryGoblinSince, // "почетный гоблин с 2015 года" — matches the profile bio
                AwardedByUserId = solohin.UserId,
            });
            result.Details.Add("TestHonorary awarded: Почетный гоблин");
        }
    }

    /// <summary>
    /// A type the award catalog hides, held by nobody: the list of types reads a hidden row with
    /// it, and the form that grants an award does not offer it. The catalog ships every type shown.
    /// </summary>
    internal static readonly Guid HiddenAwardTypeId = FixedGuid(0xa1, 1);

    private async Task SeedHiddenAwardType(ComprehensiveSeedResult result)
    {
        if (await _dbContext.AwardTypes.AnyAsync(t => t.AwardTypeId == HiddenAwardTypeId))
        {
            result.Details.Add("Hidden award type already exists, skipping");
            return;
        }

        var lastSortOrder = await _dbContext.AwardTypes.MaxAsync(t => (int?)t.SortOrder) ?? 0;
        _dbContext.AwardTypes.Add(new AwardType
        {
            AwardTypeId = HiddenAwardTypeId,
            Code = "beta_tester",
            Title = "Бета-тестер",
            Description = "Участие в закрытом тесте прошлой версии сайта",
            IconName = "ribbon-medal",
            Tier = 1,
            SortOrder = lastSortOrder + 1,
            IsActive = false
        });
        await _dbContext.SaveChangesAsync();
        result.Details.Add("Hidden award type: Бета-тестер");
    }

    private async Task SeedSolohinLexAvatar(DbUser solohin)
    {
        // Skipped when SolohinLex already has an avatar.
        if (solohin.AvatarUploadId == null)
        {
            var imageBytes = ReadEmbeddedSeedBytes(SolohinLexImage);
            var uploadId = _guidFactory.Create();
            var upload = await SeedImageFromBytesAsync(
                imageBytes,
                declaredContentType: "image/jpeg",
                sourceFileName: "SolohinLex.jpg",
                type: UploadType.UserAvatar,
                uploadId: uploadId,
                userId: solohin.UserId,
                entityId: solohin.UserId,
                uploadedUtc: _now);
            _dbContext.Uploads.Add(upload);
            solohin.AvatarUploadId = uploadId;
        }
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

    // Game posts tier ladder: T1=100, T2=500, T3=2000, T4=5000.
    private const int ArchivePostTarget = 5000;

    private const string ArchivePostMarker = "Архивная запись №";

    /// <summary>
    /// Boosts SolohinLex metrics so his achievements page shows all 5 tier plaques, platinum (T4) of the
    /// "Игровые посты" chain included: archive posts up to the T4 threshold, after which QuantityRating is
    /// recounted from the posts. Idempotent by the GameText prefix. Runs after
    /// <see cref="RecomputeUserRatings"/>, which would overwrite the QualityRating boost.
    /// </summary>
    private async Task BoostSolohinLexMetrics(ComprehensiveSeedResult result)
    {
        var solohin = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == PrimaryLogin);
        if (solohin == null)
        {
            result.Details.Add("SolohinLex metrics boost skipped: user not found");
            return;
        }

        var existing = await _dbContext.Posts
            .CountAsync(p => p.AuthorId == solohin.UserId
                && !p.IsRemoved
                && p.GameText.StartsWith(ArchivePostMarker));
        var toCreate = ArchivePostTarget - existing;
        if (toCreate > 0)
        {
            await SeedSolohinLexArchivePosts(solohin, existing, toCreate, result);
        }

        // QuantityRating, the real post count: T4. QualityRating 350: silver.
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QuantityRating" = (SELECT COUNT(*) FROM "Posts" p
                                    WHERE p."AuthorId" = u."UserId" AND p."IsRemoved" = false),
                "QualityRating" = 350
            WHERE u."Username" = 'SolohinLex';
        """);

        result.Details.Add(
            $"SolohinLex metrics boosted: +{Math.Max(toCreate, 0)} archive game posts " +
            $"(target {ArchivePostTarget} → posts T4 platinum), rating=350 (silver)");
    }

    /// <summary>
    /// The archive posts of SolohinLex, short master posts in a finished game he
    /// masters, numbered on from the ones already there.
    /// </summary>
    private async Task SeedSolohinLexArchivePosts(
        DbUser solohin, int existing, int toCreate, ComprehensiveSeedResult result)
    {
        // A finished game he masters is a plausible home for thousands of archive posts.
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

        if (game == null || room == null)
        {
            result.Details.Add("SolohinLex archive posts skipped: no finished mastered game with a room");
        }
        else
        {
            // Master posts between activation and closing, always in the past, out of the "recent activity" widgets.
            var windowStart = game.ActivatedUtc ?? game.CreatedUtc;
            var windowEnd = game.ClosedUtc ?? _now.AddDays(-1);
            if (windowEnd <= windowStart) windowEnd = windowStart.AddDays(30);
            var stepTicks = (windowEnd - windowStart).Ticks / ArchivePostTarget;

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
                    GameText = $"{ArchivePostMarker}{ordinal}. Летопись кампании, сохранена для истории.",
                });
            }
            _dbContext.Posts.AddRange(posts);
            await _dbContext.SaveChangesAsync();
            result.PostsCreated += toCreate;
        }
    }
}
