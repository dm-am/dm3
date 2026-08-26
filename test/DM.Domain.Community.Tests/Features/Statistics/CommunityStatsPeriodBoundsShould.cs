using System;
using DM.Domain.Community.Features.Statistics;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Statistics;

/// <summary>
/// Covers the stats period-window resolution: the new "all-time" period and
/// the correctness of calendar year / month windows.
/// </summary>
public class CommunityStatsPeriodBoundsShould
{
    [Fact]
    public void ReturnWidestWindowForAllTimePeriod()
    {
        // year == 0 is the all-time period: no row should be excluded by date.
        var (start, end) = CommunityStatsService.ResolvePeriodBounds(0, null);

        start.Should().Be(DateTimeOffset.MinValue);
        end.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public void ReturnWidestWindowForAllTimeEvenWithMonth()
    {
        // A month alongside year 0 is still all-time (month is ignored).
        var (start, end) = CommunityStatsService.ResolvePeriodBounds(0, 6);

        start.Should().Be(DateTimeOffset.MinValue);
        end.Should().Be(DateTimeOffset.MaxValue);
    }

    [Fact]
    public void ReturnCalendarYearWindow()
    {
        var (start, end) = CommunityStatsService.ResolvePeriodBounds(2024, null);

        start.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ReturnRequestedMonthWindow()
    {
        // "month" must mean exactly the requested calendar month.
        var (start, end) = CommunityStatsService.ResolvePeriodBounds(2024, 3);

        start.Should().Be(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RollRequestedDecemberIntoNextYear()
    {
        var (start, end) = CommunityStatsService.ResolvePeriodBounds(2024, 12);

        start.Should().Be(new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
