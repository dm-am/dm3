using System;
using AwesomeAssertions;
using DM.Domain.Core.Dates;
using Xunit;

namespace DM.Domain.Core.Tests.Dates;

/// <summary>
/// A moment written the way the reader of a letter keeps time.
/// </summary>
/// <remarks>
/// The site formats a date in the browser, which knows where its reader is. A
/// letter has no browser, so the account carries the zone and this turns it into
/// the line a letter prints. Everything below is about the line being either
/// right or openly UTC: a letter that quietly guessed the zone would be wrong by
/// hours with nothing on the page admitting it.
/// </remarks>
public class ReaderTimeShould
{
    private static readonly DateTimeOffset Moment =
        new(2026, 9, 21, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void WriteTheMomentInTheZoneOfTheReader()
    {
        ReaderTime.Format(Moment, "Europe/Moscow").Should().Be("21.09.2026 13:30 (UTC+3)");
    }

    /// <summary>
    /// The offset travels with the time. A zone can be stale — the reader moved
    /// and has not said so — and named, the time is one the reader can convert.
    /// </summary>
    [Theory]
    [InlineData("Europe/Moscow", "21.09.2026 13:30 (UTC+3)")]
    [InlineData("Asia/Kolkata", "21.09.2026 16:00 (UTC+5:30)")]
    [InlineData("America/New_York", "21.09.2026 06:30 (UTC-4)")]
    [InlineData("Europe/London", "21.09.2026 11:30 (UTC+1)")]
    public void NameTheOffsetBesideTheTime(string zone, string expected)
    {
        ReaderTime.Format(Moment, zone).Should().Be(expected);
    }

    /// <summary>UTC is named UTC and carries no zero to add up.</summary>
    [Fact]
    public void WriteUtcWithoutAnOffset()
    {
        ReaderTime.Format(Moment, "UTC").Should().Be("21.09.2026 10:30 (UTC)");
    }

    /// <summary>
    /// No zone is UTC, which is what every account had before there was a zone
    /// to keep.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FallBackToUtcWhenTheAccountNamesNoZone(string? zone)
    {
        ReaderTime.Format(Moment, zone).Should().Be("21.09.2026 10:30 (UTC)");
    }

    /// <summary>
    /// A zone this machine does not know is UTC and not an exception: the value
    /// comes from a browser, the zone database moves, and a letter about
    /// security must not fail to send over the spelling of a city.
    /// </summary>
    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("не зона")]
    public void FallBackToUtcForAZoneThisMachineDoesNotKnow(string zone)
    {
        ReaderTime.Format(Moment, zone).Should().Be("21.09.2026 10:30 (UTC)");
    }

    /// <summary>
    /// Summer time is the zone's business and not the caller's: the same zone
    /// answers differently in January and in July.
    /// </summary>
    [Fact]
    public void FollowTheSummerTimeOfTheZone()
    {
        var winter = new DateTimeOffset(2026, 1, 21, 10, 30, 0, TimeSpan.Zero);

        ReaderTime.Format(winter, "Europe/London").Should().Be("21.01.2026 10:30 (UTC)");
        ReaderTime.Format(Moment, "Europe/London").Should().Be("21.09.2026 11:30 (UTC+1)");
    }

    /// <summary>
    /// A zone an account may be given is one the browser names and a letter can
    /// print a time in: an IANA name this machine knows, and nothing else.
    /// </summary>
    [Theory]
    [InlineData("Europe/Moscow", true)]
    [InlineData(" Asia/Vladivostok ", true)]
    [InlineData("Mars/Olympus_Mons", false)]
    [InlineData("Russian Standard Time", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void KnowAZoneByItsIanaNameOnly(string? timeZoneId, bool known) =>
        ReaderTime.IsKnownZone(timeZoneId).Should().Be(known);
}
