using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Repositories.General;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using IObjectStorage = DM.Domain.Core.Uploads.IObjectStorage;

namespace DM.Infrastructure.Persistence.Tests.Repositories.General;

/// <summary>
/// The orphan sweeper looks for soft-deleted uploads, and DmDbContext applies a
/// global "not removed" query filter to every IRemovable entity. Written without
/// IgnoreQueryFilters the predicate collapses to "NOT IsRemoved AND IsRemoved",
/// which no row can satisfy: the sweep reports "no orphans" forever while every
/// deleted avatar and attachment stays in the bucket at its original public URL.
/// These tests pin both halves of the contract — a row past the grace period IS
/// collected, and rows that are fresh or live are NOT.
/// </summary>
/// <remarks>
/// The sweep used to live in a background service of the HTTP host, which is why
/// this used to build a service provider to reach it. It is a domain contract
/// now, so the test constructs the collector the way anything else would.
/// </remarks>
public class UploadOrphanCollectorShould : UnitTestBase
{
    /// <summary>
    /// The instant every row here is dated from. Fixed rather than taken from
    /// DateTimeOffset.UtcNow, so the grace period is checked against a value the
    /// test owns instead of against the same wall clock the code under test reads.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly IObjectStorage _objectStorage;
    private readonly IDateTimeProvider _clock;

    public UploadOrphanCollectorShould()
    {
        _objectStorage = Mock<IObjectStorage>();
        _objectStorage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _clock = Mock<IDateTimeProvider>();
        _clock.Now.Returns(Now);
    }

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private static Upload NewUpload(bool removed, DateTimeOffset? deletedUtc, string objectKey) => new()
    {
        UploadId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CreatedUtc = Now.AddDays(-10),
        ContentType = "image/png",
        SizeBytes = 1024,
        ObjectKey = objectKey,
        IsRemoved = removed,
        DeletedUtc = deletedUtc,
    };

    private async Task GivenUpload(Upload upload)
    {
        await using var dbContext = Context();
        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>One pass, on a context of its own as a background scope has.</summary>
    private async Task Sweep()
    {
        await using var dbContext = Context();
        await new UploadOrphanCollector(dbContext, _objectStorage, _clock)
            .SweepAsync(CancellationToken.None);
    }

    private async Task<int> RemainingUploads()
    {
        await using var dbContext = Context();
        return await dbContext.Uploads.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task CollectUploadRemovedBeyondTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddDays(-2), "uploads/expired.png"));

        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("uploads/expired.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }

    [Fact]
    public async Task LeaveUploadStillWithinTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddHours(-1), "uploads/fresh.png"));

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    [Fact]
    public async Task LeaveUploadThatIsNotDeleted()
    {
        await GivenUpload(NewUpload(removed: false, deletedUtc: null, "uploads/live.png"));

        await Sweep();

        await _objectStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(1);
    }

    /// <summary>
    /// The grace period is a statement about elapsed time, so the test states it by
    /// moving the clock: one and the same row is left alone before the deadline and
    /// collected after it, with nothing but IDateTimeProvider changing in between.
    /// </summary>
    [Fact]
    public async Task CollectUploadOnceTheClockPassesTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now, "uploads/aging.png"));

        await Sweep();
        (await RemainingUploads()).Should().Be(1);

        _clock.Now.Returns(Now.AddHours(25));
        await Sweep();

        await _objectStorage.Received(1).DeleteAsync("uploads/aging.png", Arg.Any<CancellationToken>());
        (await RemainingUploads()).Should().Be(0);
    }
}
