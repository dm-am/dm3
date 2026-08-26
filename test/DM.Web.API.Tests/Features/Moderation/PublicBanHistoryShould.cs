using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Uploads;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Features.Moderation.Warnings;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// A lifted ban stays in the moderation history and stays out of the public one.
/// </summary>
/// <remarks>
/// Two paths read the same list and they used to agree by accident: a lifted ban
/// was soft-deleted, so it was gone from both. Keeping it (which is what the
/// finding asked for) split them apart, and nothing said so. The profile prints
/// "Последний бан: N-й с ..." off History.Count, PublicBan carries neither
/// IsLifted nor LiftedUtc, and the achievement counter behind "резиновая уточка"
/// goes on not counting a ban that was taken back -- so the same lifted ban was
/// counted against the user on his profile and not counted for his achievement.
///
/// The moderation view is the opposite rule and is asserted here as well: that
/// one has the flag, and hiding a lifted ban there is the defect the finding was
/// raised over.
/// </remarks>
public class PublicBanHistoryShould : UnitTestBase
{
    private static readonly DateTimeOffset Moment = new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid ServedId = Guid.Parse("b2d4f6e8-0000-4000-8000-000000000001");
    private static readonly Guid LiftedId = Guid.Parse("b2d4f6e8-0000-4000-8000-000000000002");


    private static BanApiService Service()
    {
        var banService = Substitute.For<IBanService>();
        banService
            .GetUserBans("Alice", Arg.Any<CancellationToken>()).Returns(new List<DomainBan>
            {
                new()
                {
                    BanId = ServedId,
                    StartedUtc = Moment.AddDays(-90),
                    EndedUtc = Moment.AddDays(-60)
                },
                new()
                {
                    BanId = LiftedId,
                    StartedUtc = Moment.AddDays(-30),
                    EndedUtc = Moment.AddDays(-1),
                    LiftedUtc = Moment.AddDays(-20)
                }
            });
        banService
            .GetActiveBan("Alice", Arg.Any<CancellationToken>()).Returns((DomainBan?)null);

        return new BanApiService(banService, WarningMappers.At(Moment));
    }

    [Fact]
    public async Task LeaveALiftedBanOutOfThePublicProfile()
    {
        var status = await Service().GetPublicUserBanStatus("Alice");

        status.History.Should().ContainSingle(
            "the profile counts the history it is given, and a ban that was taken back is not one the user served");
    }

    [Fact]
    public async Task KeepALiftedBanInTheModerationHistory()
    {
        var status = await Service().GetUserBanStatus("Alice");

        status.History.Should().HaveCount(2);
        status.History.Single(b => b.Id == LiftedId).IsLifted.Should().BeTrue(
            "the moderator is the reader who has to see that the ban was lifted, and when");
    }
}
