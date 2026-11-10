using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DM.Domain.Moderation.Features.Bans;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

// The history queries spelled the lift predicate by hand, out of sight of the model-level check.
public class BanHistoryQueryShould : UnitTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid TargetId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0001");
    private static readonly Guid ModeratorId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0002");
    private static readonly Guid LiftedBanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0003");
    private static readonly Guid RunningBanId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0004");
    private static readonly Guid OtherTargetId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0007");

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => InMemoryStore.Open(_databaseName);

    private static BanRepository Repository(DmDbContext context)
    {
        var clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Now);
        return new BanRepository(context, clock);
    }

    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.AddRange(InMemoryStore.User(TargetId, "target"), InMemoryStore.User(ModeratorId, "moderator"));
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

    // A list of bans by their start has no place for a lift both above and below the running ban.
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
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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

    // The issuer is a required navigation to a filtered account row, so the page would come up
    // short.
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

    // A new entry happens after every entry there is: it takes the next number and moves the others
    // down the list without renumbering them, so a link to an entry by its number stays a link to
    // that entry.
    [Fact]
    public async Task KeepTheNumberOfAnEntryWhenANewOneIsWritten()
    {
        using var context = Seeded();
        var newId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0008");
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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

    [Fact]
    public async Task GiveTheListOfOnePersonTheNumbersOfTheWholeList()
    {
        using var context = Seeded();
        var betweenId = Guid.Parse("2f0e2a54-0a8e-4a37-9a2a-6f1f9b0d0009");
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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

    [Fact]
    public async Task NumberEntriesOfOneMomentInTheOrderOfTheList()
    {
        using var context = Seeded();
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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

    [Fact]
    public async Task FindWhereAnEntryStandsByItsNumber()
    {
        using var context = Seeded();
        context.Users.Add(InMemoryStore.User(OtherTargetId, "other"));
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
