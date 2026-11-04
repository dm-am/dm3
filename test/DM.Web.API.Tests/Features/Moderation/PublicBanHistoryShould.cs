using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Features.Moderation.Bans;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainBan = DM.Domain.Moderation.Features.Bans.Ban;
using DM.Domain.Moderation.Features.Bans;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// A lifted ban stays in the moderation history and stays out of the public one.
/// </summary>
/// <remarks>
/// The profile counts off History.Count and PublicBan carries no lift, while the achievement does not count
/// a lifted ban; the moderation view keeps it, asserted here as well.
/// </remarks>
public class PublicBanHistoryShould
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
