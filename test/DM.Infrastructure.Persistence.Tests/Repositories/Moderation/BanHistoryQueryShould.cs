using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
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
        lifted.LiftedUtc.Should().Be(Now.AddDays(-1));
        lifted.LiftReason.Should().Be("appealed successfully");
    }

    [Fact]
    public async Task KeepALiftedBanInTheModerationHistory()
    {
        using var context = Seeded();

        var (bans, totalCount) = await Repository(context).GetBanHistory(0, 20);

        totalCount.Should().Be(2, "the count and the page describe the same set");
        bans.Should().Contain(b => b.BanId == LiftedBanId,
            "GET /v1/bans/history is the only place that shows who lifted what");
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
}
