using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Testing;
using DM.Web.API.HostedServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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
    private const string Bucket = "dm-test";

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAmazonS3> _s3;

    public UploadOrphanCleanupShould()
    {
        _s3 = Mock<IAmazonS3>();
        _s3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        var services = new ServiceCollection();
        // Scoped, exactly as in production: the service resolves its context
        // from a scope it creates and disposes itself.
        services.AddDbContext<DmDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        services.AddScoped(_ => _s3.Object);
        services.AddSingleton<IOptions<CdnConfiguration>>(
            Options.Create(new CdnConfiguration { BucketName = Bucket }));
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
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
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

    private Task Sweep() => new UploadOrphanCleanupService(
            _serviceProvider,
            NullLogger<UploadOrphanCleanupService>.Instance)
        .SweepAsync(CancellationToken.None);

    private async Task<int> RemainingUploads()
    {
        await using var dbContext = Probe();
        return await dbContext.Uploads.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task CollectUploadRemovedBeyondTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, DateTimeOffset.UtcNow.AddDays(-2), "uploads/expired.png"));

        await Sweep();

        _s3.Verify(c => c.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => r.BucketName == Bucket && r.Key == "uploads/expired.png"),
            It.IsAny<CancellationToken>()), Times.Once);
        (await RemainingUploads()).Should().Be(0);
    }

    [Fact]
    public async Task LeaveUploadStillWithinTheGracePeriod()
    {
        await GivenUpload(NewUpload(removed: true, DateTimeOffset.UtcNow.AddHours(-1), "uploads/fresh.png"));

        await Sweep();

        _s3.Verify(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await RemainingUploads()).Should().Be(1);
    }

    [Fact]
    public async Task LeaveUploadThatIsNotDeleted()
    {
        await GivenUpload(NewUpload(removed: false, deletedUtc: null, "uploads/live.png"));

        await Sweep();

        _s3.Verify(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await RemainingUploads()).Should().Be(1);
    }

    public void Dispose() => _serviceProvider.Dispose();
}
