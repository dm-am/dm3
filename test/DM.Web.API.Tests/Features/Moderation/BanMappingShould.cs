using System;
using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Features.Moderation.Warnings;
using FluentAssertions;
using Moq;
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
    private IMapper MapperAt(DateTimeOffset moment)
    {
        var clock = Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(moment);

        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
            cfg.AddProfile<WarningMappingProfile>();
        });

        return configuration.CreateMapper(type =>
        {
            if (type == typeof(BanActivityResolver))
            {
                return new BanActivityResolver(clock.Object);
            }

            if (type == typeof(BanTypeResolver))
            {
                return new BanTypeResolver(clock.Object);
            }

            return Activator.CreateInstance(type)!;
        });
    }

    [Fact]
    public void ReportABanWhoseWindowHasNotOpenedAsInactive()
    {
        var moment = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment.AddDays(1), EndedUtc = moment.AddDays(2) };

        MapperAt(moment).Map<PublicBan>(ban).IsActive.Should().BeFalse();
    }

    [Fact]
    public void ReportARunningBanAsActiveAtTheMomentTheProviderReports()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment.AddDays(-1), EndedUtc = moment.AddDays(1) };

        MapperAt(moment).Map<PublicBan>(ban).IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReportALiftedBanAsInactiveWhileItsWindowIsStillOpen()
    {
        var moment = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan
        {
            StartedUtc = moment.AddDays(-1),
            EndedUtc = moment.AddDays(1),
            IsRemoved = true
        };

        MapperAt(moment).Map<PublicBan>(ban).IsActive.Should().BeFalse();
    }

    [Fact]
    public void ReadPermanenceOffTheSameClock()
    {
        var moment = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ban = new DomainBan { StartedUtc = moment, EndedUtc = moment.AddYears(10) };

        MapperAt(moment).Map<PublicBan>(ban).Type.Should().Be(BanType.Temporary);
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

        MapperAt(moment).Map<PublicBan>(ban).Type.Should().Be(BanType.Permanent);
    }
}
