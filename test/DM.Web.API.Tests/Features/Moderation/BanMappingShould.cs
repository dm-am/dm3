using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Features.Moderation.Warnings;
using AwesomeAssertions;
using Xunit;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// "Banned right now" is one predicate — not lifted, already started, not yet
/// ended — read at the moment the injected clock reports, the same clock the ban
/// queries use. The mapping used to answer it with its own expression that had
/// no start bound and read the wall clock, so a listing and the enforcement
/// could disagree about the same ban. The moments below are deliberately far
/// from the wall clock: an implementation reading DateTimeOffset.UtcNow answers
/// the opposite in every case here.
/// </summary>
public class BanMappingShould : UnitTestBase
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

    /// <summary>
    /// Who lifted the ban, when and why reaches the moderation view. The row was
    /// soft-deleted on lifting, so no query returned it, the two audit fields were
    /// mapped with Ignore(), and IsLifted was false on everything a moderator could
    /// see.
    /// </summary>
    [Fact]
    public void CarryTheLiftAuditIntoTheModerationView()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var moderator = Guid.NewGuid();
        var ban = new DomainBan
        {
            StartedUtc = moment.AddDays(-2),
            EndedUtc = moment.AddDays(5),
            LiftedUtc = moment.AddDays(-1),
            LiftedByUserId = moderator,
            LiftReason = "Разобрались"
        };

        var mapped = WarningMappers.At(moment).ToBan(ban);

        mapped.IsLifted.Should().BeTrue();
        mapped.LiftedUtc.Should().Be(moment.AddDays(-1));
        mapped.LiftedByUserId.Should().Be(moderator);
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
