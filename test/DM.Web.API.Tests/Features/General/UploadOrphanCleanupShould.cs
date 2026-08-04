using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Testing;
using DM.Web.API.HostedServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IObjectStorage = DM.Domain.Core.Uploads.IObjectStorage;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// The orphan sweeper looks for soft-deleted uploads, and DmDbContext applies a
/// global "not removed" query filter to every IRemovable entity. Written without
/// IgnoreQueryFilters the predicate collapses to "NOT IsRemoved AND IsRemoved",
/// which no row can satisfy: the service logs "no orphans" forever while every
/// deleted avatar and attachment stays in the bucket at its original public URL.
/// These tests pin both halves of the contract — a row past the grace period IS
/// collected, and rows that are fresh or live are NOT.
/// </summary>
public class UploadOrphanCleanupShould : UnitTestBase, IDisposable
{
    /// <summary>
    /// The instant every row here is dated from. Fixed rather than taken from
    /// DateTimeOffset.UtcNow, so the grace period is checked against a value the
    /// test owns instead of against the same wall clock the code under test reads.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IObjectStorage> _objectStorage;
    private readonly Mock<IDateTimeProvider> _clock;

    public UploadOrphanCleanupShould()
    {
        _objectStorage = Mock<IObjectStorage>();
        _objectStorage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _clock = Mock<IDateTimeProvider>();
        _clock.SetupGet(c => c.Now).Returns(Now);

        var services = new ServiceCollection();
        // Scoped, exactly as in production: the periodic loop opens a scope per
        // pass and the sweeper resolves its context out of that one.
        services.AddDbContext<DmDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        services.AddScoped(_ => _objectStorage.Object);
        services.AddSingleton(_ => _clock.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>A context on the same store, independent of the service's scope.</summary>
    private DmDbContext Probe() => new(new DbContextOptionsBuilder<DmDbContext>()
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
        await using var dbContext = Probe();
        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// One pass, driven the way the periodic loop drives it: a scope per pass,
    /// opened by the caller and handed to the sweeper.
    /// </summary>
    private async Task Sweep()
    {
        using var scope = _serviceProvider.CreateScope();
        await new UploadOrphanCleanupService(
                _serviceProvider,
                NullLogger<UploadOrphanCleanupService>.Instance)
            .SweepAsync(scope.ServiceProvider, CancellationToken.None);
    }

    private async Task<int> RemainingUploads()
    {
        await using var dbContext = Probe();
        return await dbContext.Uploads.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task CollectUploadRemovedBeyondTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddDays(-2), "uploads/expired.png"));

        await Sweep();

        _objectStorage.Verify(s => s.DeleteAsync("uploads/expired.png", It.IsAny<CancellationToken>()),
            Times.Once);
        (await RemainingUploads()).Should().Be(0);
    }

    [Fact]
    public async Task LeaveUploadStillWithinTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, Now.AddHours(-1), "uploads/fresh.png"));

        await Sweep();

        _objectStorage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await RemainingUploads()).Should().Be(1);
    }

    [Fact]
    public async Task LeaveUploadThatIsNotDeleted()
    {
        await GivenUpload(NewUpload(removed: false, deletedUtc: null, "uploads/live.png"));

        await Sweep();

        _objectStorage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        _clock.SetupGet(c => c.Now).Returns(Now.AddHours(25));
        await Sweep();

        _objectStorage.Verify(s => s.DeleteAsync("uploads/aging.png", It.IsAny<CancellationToken>()),
            Times.Once);
        (await RemainingUploads()).Should().Be(0);
    }

    public void Dispose() => _serviceProvider.Dispose();
}
