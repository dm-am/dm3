using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using WarningPolicy = DM.Domain.Moderation.Features.Warnings.WarningPolicy;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The bans of the seed: SolohinLex's chain, and beside it one ban of every
    /// kind the ban list draws (<see cref="SeedBanListCases"/>).
    /// </summary>
    private async Task SeedSolohinLexBans(List<DbUser> users, ComprehensiveSeedResult result)
    {
        await SeedSolohinLexChain(users, result);
        await SeedBanListCases(users, result);
    }

    /// <summary>
    /// Seed 10 past bans for SolohinLex, issued by TestSeniorMod, the rank that bans: three tiers of the
    /// "резиновая уточка" chain are earned (BANS_1, BANS_3, BANS_10), BANS_30 stays locked. There are no
    /// voluntary bans in the seed. An eleventh, lifted early, is not counted by the
    /// chain. Idempotent.
    /// </summary>
    private async Task SeedSolohinLexChain(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        var author = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        if (solohin == null || author == null)
        {
            result.Details.Add("SolohinLex bans skipped: target or author user not found");
            return;
        }

        var existing = await _dbContext.Set<Ban>()
            .Where(b => b.TargetUserId == solohin.UserId)
            .CountAsync();
        if (existing >= 10)
        {
            result.Details.Add($"SolohinLex already has {existing} bans, skipping ban seed");
            return;
        }

        const int targetCount = 10;
        var toCreate = targetCount - existing;
        // Notes as the moderators of DM2 wrote them in its ban log, newest first.
        var reasons = new[]
        {
            "Десятый по сумме баллов (с учетом сгорания банов).",
            "По сумме баллов (9-й с учетом откатов)",
            "Восьмой по сумме баллов.",
            "Седьмой по сумме баллов (с учетом сгорания банов).",
            "6-й по сумме баллов",
            "Пятый по сумме баллов.",
            "По сумме баллов (4-й)",
            "Третий по сумме баллов.",
            "Второй по сумме баллов.",
            "Первый по сумме баллов.",
        };
        for (var i = 0; i < toCreate; i++)
        {
            var ago = (existing + i + 1) * 30; // spread evenly over recent months
            _dbContext.Set<Ban>().Add(new Ban
            {
                BanId = _guidFactory.Create(),
                TargetUserId = solohin.UserId,
                AuthorId = author.UserId,
                StartedUtc = _now.AddDays(-ago),
                EndedUtc = _now.AddDays(-ago + 7),
                Comment = reasons[(existing + i) % reasons.Length],
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
            });
        }

        // One more, lifted early, for the ban list: a mistake made in ignorance of the rules.
        // Not one of the ten: the chain counts only bans served.
        _dbContext.Set<Ban>().Add(new Ban
        {
            BanId = _guidFactory.Create(),
            TargetUserId = solohin.UserId,
            AuthorId = author.UserId,
            StartedUtc = _now.AddDays(-14),
            EndedUtc = _now.AddDays(-7),
            Comment = "Реклама стороннего сайта в глобальном чате",
            AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
            LiftedUtc = _now.AddDays(-12),
            LiftedByUserId = author.UserId,
            LiftReason = "Ссылку дал по незнанию правил, сам ее удалил и извинился: бан снят досрочно",
        });
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex bans seeded: {toCreate} created (total {targetCount}) for \"резиновая уточка\" demo, and one lifted early");
    }

    /// <summary>
    /// The bans the ban list needs beside SolohinLex's chain, one of each kind it
    /// draws: a permanent full ban from an administrator, a running ordinary one
    /// from a senior moderator, the site's automatic ban, filed under the system
    /// account, and a ban another moderator lifted early.
    /// </summary>
    /// <remarks>
    /// Fixed identifiers: drawn from the seeded factory, they would shift every identifier drawn after them.
    /// The permanent full ban keeps LongestLoginPossible from signing in.
    /// </remarks>
    private async Task SeedBanListCases(List<DbUser> users, ComprehensiveSeedResult result)
    {
        if (await _dbContext.Set<Ban>().AnyAsync(b => BanListCaseIds.Contains(b.BanId)))
        {
            result.Details.Add("Ban list cases already seeded, skipping");
            return;
        }

        var admin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        var senior = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        var advertiser = users.FirstOrDefault(u => u.Username == "LongestLoginPossible");
        var offender = users.FirstOrDefault(u => u.Username == "Experienced");
        var warned = users.FirstOrDefault(u => u.Username == "Тест Елки");
        var forgiven = users.FirstOrDefault(u => u.Username == "RatingPositive");
        if (admin == null || senior == null || advertiser == null || offender == null || warned == null ||
            forgiven == null)
        {
            result.Details.Add("Ban list cases skipped: an issuer or a banned reader was not found");
            return;
        }

        var permanentStart = _now.AddDays(-3);
        var automaticStart = _now.AddDays(-10);
        _dbContext.Set<Ban>().AddRange(
            // Permanent: a hundred years, as BanService writes it (Ban.PermanentYears)
            new Ban
            {
                BanId = BanListCaseIds[0],
                TargetUserId = advertiser.UserId,
                AuthorId = admin.UserId,
                StartedUtc = permanentStart,
                EndedUtc = permanentStart.AddYears(100),
                Comment = "Рекламный бот",
                AccessRestrictionPolicy = AccessPolicy.FullBan,
            },
            // Running now: the one ban of the list that offers "Снять бан"
            new Ban
            {
                BanId = BanListCaseIds[1],
                TargetUserId = offender.UserId,
                AuthorId = senior.UserId,
                StartedUtc = _now.AddDays(-1),
                EndedUtc = _now.AddDays(6),
                Comment = "Первый по сумме баллов (с учетом сгорания банов).",
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
            },
            // The site's own on warning points, as IssueAutomaticBan writes it, long over
            new Ban
            {
                BanId = BanListCaseIds[2],
                TargetUserId = warned.UserId,
                AuthorId = SystemUser.Id,
                StartedUtc = automaticStart,
                EndedUtc = automaticStart.AddHours(WarningPolicy.AutoBanDurationHours),
                Comment = WarningPolicy.AutoBanComment,
                AccessRestrictionPolicy = WarningPolicy.AutoBanPolicy,
            },
            // Lifted by somebody other than its issuer, two days into its week
            new Ban
            {
                BanId = BanListCaseIds[3],
                TargetUserId = forgiven.UserId,
                AuthorId = senior.UserId,
                StartedUtc = _now.AddDays(-6),
                EndedUtc = _now.AddDays(1),
                Comment = "Второй по сумме баллов.",
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
                LiftedUtc = _now.AddDays(-4),
                LiftedByUserId = admin.UserId,
                LiftReason = "Извинился, договорились",
            });
        await _dbContext.SaveChangesAsync();
        result.Details.Add("Ban list cases seeded: a permanent full ban, a running ban, an automatic ban and a lifted one");
    }

    /// <summary>
    /// The fixed identifiers of <see cref="SeedBanListCases"/>, in the order it
    /// writes them.
    /// </summary>
    private static readonly Guid[] BanListCaseIds =
    [
        FixedGuid(0xb0, 1),
        FixedGuid(0xb0, 2),
        FixedGuid(0xb0, 3),
        FixedGuid(0xb0, 4),
    ];
}
