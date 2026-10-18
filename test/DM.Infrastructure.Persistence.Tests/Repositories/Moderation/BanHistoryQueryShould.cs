using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

/// <summary>
/// The two history queries answer with lifted bans, and the enforcement query
/// does not.
/// </summary>
/// <remarks>
/// Lifting used to soft-delete the row, so the ban left the user's history and the
/// moderation history the moment a senior moderator took it back: who lifted it,
/// when and why became unreadable, and IsLifted was false on every row a query
/// could still return. Removing the global filter is not the whole rule - the
/// history queries also carried the predicate written out by hand, and rewriting
/// that hand-written copy in terms of the new column brings the same disappearance
/// back with no filter in sight. The model-level check beside this one cannot see
/// a Where in a repository, which is where the copy lived.
///
/// GetActiveBan is asserted in the same place on purpose: "keep the lifted row" and
/// "do not enforce it" are one decision read from two sides, and a test for either
/// half alone is satisfied by deleting the other.
/// </remarks>
public class BanHistoryQueryShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid TargetId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0001");
    private static readonly Guid ModeratorId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0002");
    private static readonly Guid LiftedBanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0003");
    private static readonly Guid RunningBanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0004");
    private static readonly Guid OtherTargetId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0007");

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private BanRepository Repository(DmDbContext context)
    {
        var clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Now);
        return new BanRepository(context, clock);
    }

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
    };

    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.AddRange(NewUser(TargetId, "target"), NewUser(ModeratorId, "moderator"));
        context.Bans.AddRange(
            new DbBan
            {
                BanId = RunningBanId,
                TargetUserId = TargetId,
                AuthorId = ModeratorId,
                StartedUtc = Now.AddDays(-2),
                EndedUtc = Now.AddDays(2),
                Comment = "running",
            },
            new DbBan
            {
                BanId = LiftedBanId,
                TargetUserId = TargetId,
                AuthorId = ModeratorId,
                StartedUtc = Now.AddDays(-5),
                EndedUtc = Now.AddDays(5),
                Comment = "lifted early",
                LiftedByUserId = ModeratorId,
                LiftedUtc = Now.AddDays(-1),
                LiftReason = "appealed successfully",
            });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task KeepALiftedBanInTheUserHistory()
    {
        using var context = Seeded();

        var bans = (await Repository(context).GetUserBans(TargetId)).ToArray();

        var lifted = bans.Should().ContainSingle(b => b.BanId == LiftedBanId,
            "a lifted ban is what the user's ban history is for").Subject;
        lifted.IsLifted.Should().BeTrue();
        lifted.LiftedByUserId.Should().Be(ModeratorId);
        lifted.LiftedBy!.Username.Should().Be("moderator");
        lifted.LiftedUtc.Should().Be(Now.AddDays(-1));
        lifted.LiftReason.Should().Be("appealed successfully");
    }

    [Fact]
    public async Task KeepALiftedBanInTheModerationHistory()
    {
        using var context = Seeded();

        var (entries, totalCount) = await Repository(context).GetBanHistory(null, 0, 20);

        totalCount.Should().Be(3, "the count and the page describe the same set");
        entries.Should().Contain(e => e.Ban.BanId == LiftedBanId && e.Kind == BanLogEntryKind.Lifted,
            "GET /v1/bans/history is the only place that shows who lifted what");
        entries.First(e => e.Ban.BanId == LiftedBanId).Ban.LiftedBy!.UserId.Should().Be(ModeratorId,
            "the list signs the lift with whoever lifted it");
        entries.Single(e => e.Ban.BanId == RunningBanId).Ban.LiftedBy.Should().BeNull();
    }

    /// <summary>
    /// The list is the ban log: an issue at its start and a lift at its moment,
    /// newest first, and the lift of an older ban above the issue of a newer one.
    /// </summary>
    /// <remarks>
    /// The lifted ban started before the running one and was lifted after it
    /// started, so a list of bans by their start, which the history used to be,
    /// has no place for the lift that is both above and below the running ban.
    /// </remarks>
    [Fact]
    public async Task ListAnIssueAndALiftAsEntriesOfTheirOwnNewestFirst()
    {
        using var context = Seeded();

        var (entries, totalCount) = await Repository(context).GetBanHistory(null, 0, 20);

        entries.Select(e => (e.Kind, e.Ban.BanId, e.OccurredUtc)).Should().Equal(
            (BanLogEntryKind.Lifted, LiftedBanId, Now.AddDays(-1)),
            (BanLogEntryKind.Issued, RunningBanId, Now.AddDays(-2)),
            (BanLogEntryKind.Issued, LiftedBanId, Now.AddDays(-5)));
        totalCount.Should().Be(3, "the page counts entries, not bans");
    }

    /// <summary>
    /// A lift and an issue of the same moment: the lift first, as the later word.
    /// </summary>
    [Fact]
    public async Task PutALiftBeforeAnIssueOfTheSameMoment()
    {
        using var context = Seeded();
        var sameMomentId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0005");
        context.Bans.Add(new DbBan
        {
            BanId = sameMomentId,
            TargetUserId = OtherTargetId,
            AuthorId = ModeratorId,
            StartedUtc = Now.AddDays(-1),
            EndedUtc = Now.AddDays(6),
            Comment = "issued the moment the other was lifted",
        });
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.SaveChanges();

        var (entries, _) = await Repository(context).GetBanHistory(null, 0, 2);

        entries.Select(e => (e.Kind, e.Ban.BanId)).Should().Equal(
            (BanLogEntryKind.Lifted, LiftedBanId),
            (BanLogEntryKind.Issued, sameMomentId));
    }

    /// <summary>
    /// The list of one person leaves everybody else out, in the page and in the
    /// count, and a page is a slice of entries rather than of bans.
    /// </summary>
    [Fact]
    public async Task NarrowTheListToOnePersonAndPageItByEntries()
    {
        using var context = Seeded();
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.Bans.Add(new DbBan
        {
            BanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0006"),
            TargetUserId = OtherTargetId,
            AuthorId = ModeratorId,
            StartedUtc = Now.AddHours(-1),
            EndedUtc = Now.AddDays(6),
            Comment = "somebody else",
        });
        context.SaveChanges();

        var (entries, totalCount) = await Repository(context).GetBanHistory(TargetId, 1, 1);

        totalCount.Should().Be(3, "three entries are the target's, the fourth is somebody else's");
        entries.Select(e => (e.Kind, e.Ban.BanId)).Should().Equal((BanLogEntryKind.Issued, RunningBanId));
    }

    [Fact]
    public async Task NotEnforceALiftedBan()
    {
        using var context = Seeded();

        var active = await Repository(context).GetActiveBan(TargetId);

        active.Should().NotBeNull();
        active!.BanId.Should().Be(RunningBanId,
            "the lifted ban is kept for the history and is in force for nobody");
    }

    /// <summary>
    /// The bans of a moderator who removed his account since stay in the list,
    /// issue and lift alike, and are signed by nobody, as a removed author is
    /// everywhere, and not by the name of the account.
    /// </summary>
    /// <remarks>
    /// The issuer is a required navigation, joined to an account row the filter
    /// of removed accounts hides. The count and the numbers count the entries of
    /// such a ban, so the page holds them too: with the filter in force the page
    /// comes up short and a number leads to an entry that is not on it.
    /// </remarks>
    [Fact]
    public async Task SignTheBansOfAModeratorWhoIsGoneByNobody()
    {
        using var context = Seeded();
        context.Users.Single(u => u.UserId == ModeratorId).IsRemoved = true;
        context.SaveChanges();

        var (entries, totalCount) = await Repository(context).GetBanHistory(null, 0, 20);

        entries.Should().HaveCount(totalCount, "the page holds every entry the list counts");
        entries.Select(e => e.Number).Should().Equal(3, 2, 1);
        entries.Should().OnlyContain(e => e.Ban.Author == null);
        var lift = entries.Single(e => e.Kind == BanLogEntryKind.Lifted).Ban;
        lift.IsLifted.Should().BeTrue();
        lift.LiftedBy.Should().BeNull();
    }

    /// <summary>
    /// An entry is numbered in the whole list from the oldest, which is 1, so
    /// the list, newest first, counts down.
    /// </summary>
    [Fact]
    public async Task NumberTheEntriesFromTheOldest()
    {
        using var context = Seeded();

        var (entries, _) = await Repository(context).GetBanHistory(null, 0, 20);

        entries.Select(e => (e.Kind, e.Ban.BanId, e.Number)).Should().Equal(
            (BanLogEntryKind.Lifted, LiftedBanId, 3),
            (BanLogEntryKind.Issued, RunningBanId, 2),
            (BanLogEntryKind.Issued, LiftedBanId, 1));
    }

    /// <summary>
    /// A new entry happens after every entry there is: it takes the next number
    /// and moves the others down the list without renumbering them, so a link to
    /// an entry by its number stays a link to that entry.
    /// </summary>
    [Fact]
    public async Task KeepTheNumberOfAnEntryWhenANewOneIsWritten()
    {
        using var context = Seeded();
        var newId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0008");
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.Bans.Add(new DbBan
        {
            BanId = newId,
            TargetUserId = OtherTargetId,
            AuthorId = ModeratorId,
            StartedUtc = Now,
            EndedUtc = Now.AddDays(7),
            Comment = "the newest",
        });
        context.SaveChanges();

        var (entries, _) = await Repository(context).GetBanHistory(null, 1, 3);

        entries.Select(e => (e.Kind, e.Ban.BanId, e.Number)).Should().Equal(
            (BanLogEntryKind.Lifted, LiftedBanId, 3),
            (BanLogEntryKind.Issued, RunningBanId, 2),
            (BanLogEntryKind.Issued, LiftedBanId, 1));
        (await Repository(context).GetBanHistory(null, 0, 1)).Entries.Single().Number.Should().Be(4);
    }

    /// <summary>
    /// The list of one person shows the numbers of the whole list: an entry of
    /// somebody else between two of his is counted, and is not on his page.
    /// </summary>
    [Fact]
    public async Task GiveTheListOfOnePersonTheNumbersOfTheWholeList()
    {
        using var context = Seeded();
        var betweenId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0009");
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.Bans.Add(new DbBan
        {
            BanId = betweenId,
            TargetUserId = OtherTargetId,
            AuthorId = ModeratorId,
            StartedUtc = Now.AddDays(-3),
            EndedUtc = Now.AddDays(7),
            Comment = "somebody else, between the two",
        });
        context.SaveChanges();

        var (entries, _) = await Repository(context).GetBanHistory(TargetId, 0, 20);

        entries.Select(e => (e.Kind, e.Ban.BanId, e.Number)).Should().Equal(
            (BanLogEntryKind.Lifted, LiftedBanId, 4),
            (BanLogEntryKind.Issued, RunningBanId, 3),
            (BanLogEntryKind.Issued, LiftedBanId, 1));
    }

    /// <summary>
    /// Entries of one moment are numbered in the order the list stands them in:
    /// the lift above the issue has the greater number, and of two issues the one
    /// the list puts first does too.
    /// </summary>
    [Fact]
    public async Task NumberEntriesOfOneMomentInTheOrderOfTheList()
    {
        using var context = Seeded();
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.Bans.AddRange(
            new DbBan
            {
                BanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0005"),
                TargetUserId = OtherTargetId,
                AuthorId = ModeratorId,
                StartedUtc = Now.AddDays(-1),
                EndedUtc = Now.AddDays(6),
                Comment = "issued the moment the other was lifted",
            },
            new DbBan
            {
                BanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d000a"),
                TargetUserId = OtherTargetId,
                AuthorId = ModeratorId,
                StartedUtc = Now.AddDays(-1),
                EndedUtc = Now.AddDays(6),
                Comment = "issued at that moment as well",
            });
        context.SaveChanges();

        var repository = Repository(context);
        var (entries, _) = await repository.GetBanHistory(null, 0, 20);

        entries.Select(e => e.Number).Should().Equal(5, 4, 3, 2, 1);
        (await repository.GetBanHistoryPosition(null, 4)).Should().Be(1);
        (await repository.GetBanHistoryPosition(null, 3)).Should().Be(2,
            "the second of two issues of one moment stands where the list puts it");
    }

    /// <summary>
    /// The page of an entry is found by its number: in the whole list, and in
    /// the list of the person it is about, where the position counts his entries
    /// only. An entry that is not in the list has no position.
    /// </summary>
    [Fact]
    public async Task FindWhereAnEntryStandsByItsNumber()
    {
        using var context = Seeded();
        context.Users.Add(NewUser(OtherTargetId, "other"));
        context.Bans.Add(new DbBan
        {
            BanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0009"),
            TargetUserId = OtherTargetId,
            AuthorId = ModeratorId,
            StartedUtc = Now.AddDays(-3),
            EndedUtc = Now.AddDays(7),
            Comment = "somebody else, between the two",
        });
        context.SaveChanges();
        var repository = Repository(context);

        // The whole list: 4 lift, 3 running, 2 somebody else's, 1 the lifted ban
        (await repository.GetBanHistoryPosition(null, 4)).Should().Be(0);
        (await repository.GetBanHistoryPosition(null, 2)).Should().Be(2);
        (await repository.GetBanHistoryPosition(null, 1)).Should().Be(3);

        // His list: 4 lift, 3 running, 1 the lifted ban
        (await repository.GetBanHistoryPosition(TargetId, 3)).Should().Be(1);
        (await repository.GetBanHistoryPosition(TargetId, 1)).Should().Be(2);
        (await repository.GetBanHistoryPosition(TargetId, 2)).Should().BeNull(
            "the entry of that number is somebody else's");

        (await repository.GetBanHistoryPosition(null, 5)).Should().BeNull("there is no fifth entry");
        (await repository.GetBanHistoryPosition(null, 0)).Should().BeNull("the numbers start at 1");
    }
}
