using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Exceptions;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Statistics;

/// <summary>
/// A period the boards cannot be counted over is refused field by field, in
/// the words of the refusal list.
/// </summary>
public class LeaderboardPeriodShould
{
    [Theory]
    [InlineData(LeaderboardPeriod.AllTime, null)]
    [InlineData(LeaderboardPeriod.MinYear, 1)]
    [InlineData(LeaderboardPeriod.MaxYear, 12)]
    public void AcceptACountablePeriod(int year, int? month) =>
        LeaderboardPeriod.Refusals(year, month).Should().BeEmpty();

    [Theory]
    [InlineData(LeaderboardPeriod.MinYear - 1)]
    [InlineData(LeaderboardPeriod.MaxYear + 1)]
    public void RefuseAYearOutsideTheCalendar(int year) =>
        LeaderboardPeriod.Refusals(year, null).Should().ContainKey("year")
            .WhoseValue.Should().Be(RefusalMessage.PeriodYearOutOfRange(
                LeaderboardPeriod.MinYear, LeaderboardPeriod.MaxYear));

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RefuseAMonthOutsideTheYear(int month) =>
        LeaderboardPeriod.Refusals(2024, month).Should().ContainKey("month")
            .WhoseValue.Should().Be(RefusalMessage.PeriodMonthOutOfRange);

    [Fact]
    public void RefuseAMonthOfAllTime() =>
        LeaderboardPeriod.Refusals(LeaderboardPeriod.AllTime, 6).Should().ContainKey("year")
            .WhoseValue.Should().Be(RefusalMessage.PeriodMonthNeedsYear);
}
