using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

/// <summary>
/// The list of violators: the points and the running bans of every reader who
/// has either.
/// </summary>
public class ViolatorListShould : WarningServiceTestBase
{
    [Fact]
    public async Task ThrowWhenNonModeratorTriesToViewViolators()
    {
        Authenticate(Guid.NewGuid(), UserRole.RegularUser, "User");

        var act = () => service.GetViolators();

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Список нарушителей доступен модераторам");
    }

    [Fact]
    public async Task MergeWarningPointsAndActiveBansIntoViolators()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };
        var bothUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Both" };

        warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>()).Returns(
            [
                new UserWarningSummary { User = pointsUser, Points = 2, LastWarningUtc = now.AddDays(-1) },
                new UserWarningSummary { User = bothUser, Points = 5, LastWarningUtc = now.AddDays(-2) }
            ]);
        banRepository.GetAllActiveBans(Arg.Any<CancellationToken>()).Returns(
            [
                new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = now.AddDays(3) },
                new Ban { BanId = Guid.NewGuid(), TargetUser = bothUser, EndedUtc = now.AddDays(7) }
            ]);

        var result = (await service.GetViolators()).ToList();

        result.Should().HaveCount(3);
        // Default sort: points descending, banned user without points goes last
        result[0].User.Username.Should().Be("Both");
        result[0].Points.Should().Be(5);
        result[0].ActiveBan.Should().NotBeNull();
        result[1].User.Username.Should().Be("PointsOnly");
        result[1].Points.Should().Be(2);
        result[1].ActiveBan.Should().BeNull();
        result[2].User.Username.Should().Be("BannedOnly");
        result[2].Points.Should().Be(0);
        result[2].ActiveBan.Should().NotBeNull();
    }

    [Fact]
    public async Task FilterViolatorsByBannedAndPointsOnly()
    {
        var pointsUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "PointsOnly" };
        var bannedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "BannedOnly" };

        warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>())
            .Returns([new UserWarningSummary { User = pointsUser, Points = 3, LastWarningUtc = now }]);
        banRepository.GetAllActiveBans(Arg.Any<CancellationToken>())
            .Returns([new Ban { BanId = Guid.NewGuid(), TargetUser = bannedUser, EndedUtc = now.AddDays(1) }]);

        var banned = (await service.GetViolators(ViolatorsFilter.Banned)).ToList();
        var pointsOnly = (await service.GetViolators(ViolatorsFilter.PointsOnly)).ToList();

        banned.Should().ContainSingle(v => v.User.Username == "BannedOnly");
        pointsOnly.Should().ContainSingle(v => v.User.Username == "PointsOnly");
    }

    /// <summary>
    /// The running ban of a violator says whether the reader may lift it, by the
    /// rule the lift is refused by, as every other ban list does.
    /// </summary>
    /// <remarks>
    /// The bans of this list are read from the repository past BanService, and the
    /// flag stayed false for everybody.
    /// </remarks>
    [Fact]
    public async Task MarkTheRunningBanOfAViolatorWithWhetherTheReaderMayLiftIt()
    {
        var liftable = new Ban
        {
            BanId = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Liftable" },
            EndedUtc = now.AddDays(3)
        };
        var permanent = new Ban
        {
            BanId = Guid.NewGuid(),
            TargetUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "Permanent" },
            EndedUtc = now.AddYears(Ban.PermanentYears)
        };
        warningRepository.GetActiveWarningSummaries(Arg.Any<CancellationToken>()).Returns([]);
        banRepository.GetAllActiveBans(Arg.Any<CancellationToken>()).Returns([liftable, permanent]);
        banService.MayLift(liftable).Returns(true);
        banService.MayLift(permanent).Returns(false);

        var violators = (await service.GetViolators()).ToList();

        violators.Single(v => v.User.Username == "Liftable").ActiveBan!.CanLift.Should().BeTrue();
        violators.Single(v => v.User.Username == "Permanent").ActiveBan!.CanLift.Should().BeFalse();
    }
}
