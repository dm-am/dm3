using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Shared.UnreadCounters;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared;

/// <summary>
/// A database that cannot be reached does not undo a commit that already
/// happened.
/// </summary>
/// <remarks>
/// Every caller of the three adjusting writes reaches them after its own write
/// has been committed, and the increment is not part of that transaction. So an
/// exception from here used to travel back up through a service that had already
/// committed and answer the caller with a failure for work that was done: the
/// reader saw their own post on the page beside an error saying it was not saved,
/// and a retry wrote it a second time.
///
/// The creating and removing writes are the opposite case and are asserted as
/// such. A marker that was never created is not a wrong number - it is an entity
/// nobody is counting at all - and the reservation that puts those writes before
/// the relational row exists so that the failure arrives while the row can still
/// be rolled back. Swallowing them would quietly destroy that ordering.
///
/// An unreachable address rather than a mock of the provider: what has to hold is
/// that a real failure of a real client does not escape, and a mock would only
/// prove that a thrown exception is caught.
/// </remarks>
public class UnreadCountersDurabilityShould
{
    /// <summary>Address nothing listens on, with the driver told to give up quickly.</summary>
    private const string Unreachable = "Host=127.0.0.1;Port=1;Database=dm3;Username=x;Password=x;Timeout=1";

    [Fact]
    public async Task KeepTheAdjustmentsToItselfWhenTheStoreIsUnreachable()
    {
        var repository = Repository();
        var entityId = Guid.NewGuid();

        await repository.Awaiting(r => r.IncrementAsync(entityId, UnreadEntryType.Message))
            .Should().NotThrowAsync("the post this counts is already committed and answered");
        await repository.Awaiting(r => r.IncrementExcludingAsync(entityId, UnreadEntryType.Message, Guid.NewGuid()))
            .Should().NotThrowAsync("same commit, same answer, same caller");
        await repository.Awaiting(r => r.DecrementAsync(entityId, UnreadEntryType.Message, DateTimeOffset.UtcNow))
            .Should().NotThrowAsync("a removal that already happened cannot be undone from here either");
    }

    [Fact]
    public async Task StillRefuseToCreateAMarkerItCouldNotWrite()
    {
        var repository = Repository();
        var entityId = Guid.NewGuid();

        await repository.Awaiting(r => r.CreateMarkerAsync(entityId, entityId, UnreadEntryType.Message))
            .Should().ThrowAsync<Exception>(
                "this write runs before the row it belongs to exists, precisely so that its " +
                "failure can still roll that row back");
        await repository.Awaiting(r => r.DeleteAsync(entityId, UnreadEntryType.Message))
            .Should().ThrowAsync<Exception>(
                "a marker left behind for a deleted entity keeps counting something that is " +
                "not there");
    }

    [Fact]
    public async Task CountTheAdjustmentItGaveUpOn()
    {
        var lost = new List<string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Name == "dm.storage.write_lost")
                {
                    self.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "operation")
                {
                    lost.Add(tag.Value?.ToString());
                }
            }
        });
        listener.Start();

        await Repository().IncrementAsync(Guid.NewGuid(), UnreadEntryType.Message);

        lost.Should().Contain("unread_counters.increment",
            "swallowing is only allowed while it is counted: the request succeeded, so this " +
            "counter and the entry beside it are the whole of what says a badge is now wrong");
    }

    private static UnreadCountersRepository Repository()
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(Unreachable)
            .Options);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        return new UnreadCountersRepository(
            context,
            clock,
            Substitute.For<ILogger<UnreadCountersRepository>>());
    }
}
