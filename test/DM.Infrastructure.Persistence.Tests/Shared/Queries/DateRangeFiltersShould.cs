using System;
using System.Linq;
using DM.Infrastructure.Persistence.Shared.Queries;
using AwesomeAssertions;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Tests.Shared.Queries;

public class DateRangeFiltersShould
{
    private static readonly DateTimeOffset Day = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IncludeWholeSelectedDayForDateOnlyBound()
    {
        var topics = new[]
        {
            Topic(Day.AddTicks(-1)),
            Topic(Day),
            Topic(Day.AddHours(23).AddMinutes(59)),
            Topic(Day.AddDays(1))
        }.AsQueryable();

        var result = topics.WhereAtOrBefore(t => t.CreatedUtc, Day).ToArray();

        result.Select(t => t.CreatedUtc).Should().BeEquivalentTo(new[]
        {
            Day.AddTicks(-1),
            Day,
            Day.AddHours(23).AddMinutes(59)
        });
    }

    [Fact]
    public void KeepInclusiveComparisonForBoundWithTimeComponent()
    {
        var bound = Day.AddHours(12);
        var topics = new[] { Topic(bound), Topic(bound.AddTicks(1)) }.AsQueryable();

        var result = topics.WhereAtOrBefore(t => t.CreatedUtc, bound).ToArray();

        result.Should().ContainSingle().Which.CreatedUtc.Should().Be(bound);
    }

    [Fact]
    public void IncludeWholeSelectedDayForOptionalDateOnlyBound()
    {
        var games = new[]
        {
            Game(null),
            Game(Day.AddHours(1)),
            Game(Day.AddDays(1))
        }.AsQueryable();

        var result = games.WhereAtOrBefore(g => g.ActivatedUtc, Day).ToArray();

        result.Should().ContainSingle().Which.ActivatedUtc.Should().Be(Day.AddHours(1));
    }

    [Fact]
    public void KeepInclusiveComparisonForOptionalBoundWithTimeComponent()
    {
        var bound = Day.AddHours(12);
        var games = new[] { Game(null), Game(bound), Game(bound.AddTicks(1)) }.AsQueryable();

        var result = games.WhereAtOrBefore(g => g.ActivatedUtc, bound).ToArray();

        result.Should().ContainSingle().Which.ActivatedUtc.Should().Be(bound);
    }

    [Fact]
    public void ExtendDateOnlyBoundWithinItsOwnOffset()
    {
        // The whole day is the user's local day, not the UTC one
        var localMidnight = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var topics = new[]
        {
            Topic(new DateTimeOffset(2026, 7, 1, 20, 59, 0, TimeSpan.Zero)), // July 1, 23:59 local
            Topic(new DateTimeOffset(2026, 7, 1, 21, 0, 0, TimeSpan.Zero)),  // July 2, 00:00 local
        }.AsQueryable();

        var result = topics.WhereAtOrBefore(t => t.CreatedUtc, localMidnight).ToArray();

        result.Should().ContainSingle().Which.CreatedUtc.ToUniversalTime()
            .Should().Be(new DateTimeOffset(2026, 7, 1, 20, 59, 0, TimeSpan.Zero));
    }

    private static readonly DateTimeOffset MaxDay = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FallBackToInclusiveBoundForMaxDate()
    {
        var topics = new[] { Topic(MaxDay.AddTicks(-1)), Topic(MaxDay) }.AsQueryable();

        var result = topics.WhereAtOrBefore(t => t.CreatedUtc, MaxDay).ToArray();

        result.Select(t => t.CreatedUtc).Should().BeEquivalentTo(new[] { MaxDay.AddTicks(-1), MaxDay });
    }

    [Fact]
    public void FallBackToInclusiveOptionalBoundForMaxDate()
    {
        var games = new[] { Game(null), Game(MaxDay) }.AsQueryable();

        var result = games.WhereAtOrBefore(g => g.ActivatedUtc, MaxDay).ToArray();

        result.Should().ContainSingle().Which.ActivatedUtc.Should().Be(MaxDay);
    }

    [Fact]
    public void NotOverflowForMaxDateBoundWithPositiveOffset()
    {
        // The instant is below DateTimeOffset.MaxValue, but the clock date
        // is already the last representable day
        var localMaxDay = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.FromHours(3));
        var topics = new[] { Topic(localMaxDay), Topic(localMaxDay.AddTicks(1)) }.AsQueryable();

        var result = topics.WhereAtOrBefore(t => t.CreatedUtc, localMaxDay).ToArray();

        result.Should().ContainSingle().Which.CreatedUtc.Should().Be(localMaxDay);
    }

    [Fact]
    public void IncludeTheLowerBoundOfARange()
    {
        var from = Day.AddHours(12);
        var topics = new[] { Topic(from.AddTicks(-1)), Topic(from), Topic(from.AddTicks(1)) }.AsQueryable();

        var result = topics.WhereWithin(t => t.CreatedUtc, from, null).ToArray();

        result.Select(t => t.CreatedUtc).Should().BeEquivalentTo(new[] { from, from.AddTicks(1) });
    }

    [Fact]
    public void KeepBothBoundsOfARangeInclusive()
    {
        var from = Day.AddHours(6);
        var to = Day.AddHours(18);
        var topics = new[]
        {
            Topic(from.AddTicks(-1)), Topic(from), Topic(to), Topic(to.AddTicks(1))
        }.AsQueryable();

        var result = topics.WhereWithin(t => t.CreatedUtc, from, to).ToArray();

        result.Select(t => t.CreatedUtc).Should().BeEquivalentTo(new[] { from, to });
    }

    [Fact]
    public void KeepEveryRowWhenARangeHasNoBounds()
    {
        var topics = new[] { Topic(Day), Topic(Day.AddYears(1)) }.AsQueryable();
        var games = new[] { Game(null), Game(Day) }.AsQueryable();

        topics.WhereWithin(t => t.CreatedUtc, null, null).Should().HaveCount(2);
        games.WhereWithin(g => g.ActivatedUtc, null, null).Should().HaveCount(2);
    }

    [Fact]
    public void DropRowsWithoutTheDateAsSoonAsAnyBoundIsSet()
    {
        var games = new[] { Game(null), Game(Day.AddHours(1)) }.AsQueryable();

        var fromOnly = games.WhereWithin(g => g.ActivatedUtc, Day, null).ToArray();
        var toOnly = games.WhereWithin(g => g.ActivatedUtc, null, Day.AddDays(1)).ToArray();

        fromOnly.Should().ContainSingle().Which.ActivatedUtc.Should().Be(Day.AddHours(1));
        toOnly.Should().ContainSingle().Which.ActivatedUtc.Should().Be(Day.AddHours(1));
    }

    [Fact]
    public void IncludeTheLowerBoundOfAnOptionalDateRange()
    {
        var games = new[] { Game(Day.AddTicks(-1)), Game(Day), Game(Day.AddTicks(1)) }.AsQueryable();

        var result = games.WhereWithin(g => g.ActivatedUtc, Day, Day.AddDays(1)).ToArray();

        result.Select(g => g.ActivatedUtc).Should().BeEquivalentTo(new DateTimeOffset?[] { Day, Day.AddTicks(1) });
    }

    private static DbTopic Topic(DateTimeOffset createdUtc) => new() { CreatedUtc = createdUtc };

    private static DbGame Game(DateTimeOffset? activatedUtc) => new() { ActivatedUtc = activatedUtc };
}
