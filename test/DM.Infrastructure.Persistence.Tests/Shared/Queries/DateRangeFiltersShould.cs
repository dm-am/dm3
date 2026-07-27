using System;
using System.Linq;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Testing;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPoll = DM.Infrastructure.Persistence.Entities.Forum.Poll;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Tests.Shared.Queries;

public class DateRangeFiltersShould : UnitTestBase
{
    private static readonly DateTimeOffset Day = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    // ═══ Queryable filters (relational repositories) ═══

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

    // ═══ Mongo filters (poll list) ═══

    [Fact]
    public void RenderExclusiveNextMidnightBoundForDateOnlyPollFilter()
    {
        var filter = DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, Day);

        var rendered = Render(filter);

        rendered["EndsUtc"]["$lt"].ToUniversalTime()
            .Should().Be(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RenderInclusiveBoundForPollFilterWithTimeComponent()
    {
        var to = Day.AddHours(15);

        var filter = DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, to);

        var rendered = Render(filter);

        rendered["EndsUtc"]["$lte"].ToUniversalTime().Should().Be(to.UtcDateTime);
    }

    [Fact]
    public void ExtendDateOnlyBoundWithinItsOwnOffset()
    {
        // The whole day is the user's local day, not the UTC one
        var localMidnight = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(3));

        var filter = DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, localMidnight);

        var rendered = Render(filter);

        rendered["EndsUtc"]["$lt"].ToUniversalTime()
            .Should().Be(new DateTime(2026, 7, 1, 21, 0, 0, DateTimeKind.Utc));
    }

    // ═══ Calendar edge: whole-day bound cannot extend past the max date ═══

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
    public void RenderInclusiveBoundForMaxDatePollFilter()
    {
        var filter = DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, MaxDay);

        var rendered = Render(filter);

        rendered["EndsUtc"]["$lte"].ToUniversalTime().Should().Be(MaxDay.UtcDateTime);
    }

    [Fact]
    public void NotOverflowForMaxDateBoundWithPositiveOffset()
    {
        // The instant is below DateTimeOffset.MaxValue, but the clock date
        // is already the last representable day
        var localMaxDay = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.FromHours(3));

        var filter = DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, localMaxDay);

        var rendered = Render(filter);

        rendered["EndsUtc"]["$lte"].ToUniversalTime().Should().Be(localMaxDay.UtcDateTime);
    }

    private static BsonDocument Render(FilterDefinition<DbPoll> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        return filter.Render(registry.GetSerializer<DbPoll>(), registry);
    }

    private static DbTopic Topic(DateTimeOffset createdUtc) => new() { CreatedUtc = createdUtc };

    private static DbGame Game(DateTimeOffset? activatedUtc) => new() { ActivatedUtc = activatedUtc };
}
