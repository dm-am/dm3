using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.UnreadCounters;
using DM.Testing;
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
/// The adjusting writes run after the caller's commit and must not fail it; the creating and removing writes
/// run before the row and must fail. An unreachable address rather than a mock of the provider.
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
        using var listener = CounterTags.Listen("dm.storage.write_lost", "operation", lost);

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
