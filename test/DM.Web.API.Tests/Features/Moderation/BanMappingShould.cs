using System;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Moderation.Bans;
using AwesomeAssertions;
using Xunit;
using DomainBan = DM.Domain.Moderation.Features.Bans.Ban;

namespace DM.Web.API.Tests.Features.Moderation;

// The moments are far from the wall clock, so reading it fails every case.
public class BanMappingShould
{

    [Fact]
    public void ReportABanWhoseWindowHasNotOpenedAsInactive()
    {
        var moment = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment.AddDays(1), EndedUtc = moment.AddDays(2) };

        WarningMappers.At(moment).ToPublicBan(ban).IsActive.Should().BeFalse();
    }

    [Fact]
    public void ReportARunningBanAsActiveAtTheMomentTheProviderReports()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment.AddDays(-1), EndedUtc = moment.AddDays(1) };

        WarningMappers.At(moment).ToPublicBan(ban).IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReportALiftedBanAsInactiveWhileItsWindowIsStillOpen()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan
        {
            StartedUtc = moment.AddDays(-1),
            EndedUtc = moment.AddDays(1),
            LiftedUtc = moment
        };

        WarningMappers.At(moment).ToPublicBan(ban).IsActive.Should().BeFalse();
    }

    [Fact]
    public void CarryTheLiftAuditIntoTheModerationView()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lifterId = Guid.NewGuid();
        var ban = new DomainBan
        {
            StartedUtc = moment.AddDays(-2),
            EndedUtc = moment.AddDays(5),
            LiftedUtc = moment.AddDays(-1),
            LiftedByUserId = lifterId,
            LiftedBy = new GeneralUser { UserId = lifterId, Username = "TestSeniorMod" },
            LiftReason = "Разобрались"
        };

        var mapped = WarningMappers.At(moment).ToBan(ban);

        mapped.IsLifted.Should().BeTrue();
        mapped.LiftedUtc.Should().Be(moment.AddDays(-1));
        mapped.LiftedBy!.Username.Should().Be("TestSeniorMod", "the list signs the lift with a link to the user");
        mapped.LiftedBy.Id.Should().Be(lifterId);
        mapped.LiftReason.Should().Be("Разобрались");
    }

    [Fact]
    public void ReadPermanenceOffTheSameClock()
    {
        var moment = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment, EndedUtc = moment.AddYears(10) };

        WarningMappers.At(moment).ToPublicBan(ban).Type.Should().Be(BanType.Temporary);
    }

    [Fact]
    public void CallABanWrittenForTheFullPermanentLengthPermanent()
    {
        var moment = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan
        {
            StartedUtc = moment,
            EndedUtc = moment.AddYears(DomainBan.PermanentYears)
        };

        WarningMappers.At(moment).ToPublicBan(ban).Type.Should().Be(BanType.Permanent);
    }
}
