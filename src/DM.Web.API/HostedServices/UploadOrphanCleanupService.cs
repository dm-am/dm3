using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background worker that physically deletes S3 objects for soft-deleted uploads.
///
/// Workflow:
/// 1. Periodically (every N hours) selects Uploads.IsRemoved=true whose
///    DeletedUtc is older than the grace period (24h by default).
/// 2. For each such record deletes its single source object in S3 — thumbnail
///    variants are made on-the-fly by imgproxy and never stored, so there is
///    nothing else to delete. Does not fail on 404 (a missing file is fine) or
///    transient errors.
/// 3. Deletes the Upload record itself from the DB (hard-delete) only after all
///    S3 objects were deleted successfully. If the S3 delete failed, the record stays and
///    is retried on the next tick.
///
/// The grace period allows short-term recovery: the user clicked "удалить
/// аватар", changed their mind — restore works within the first 24h.
/// </summary>
internal class UploadOrphanCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UploadOrphanCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);
    private readonly TimeSpan _gracePeriod = TimeSpan.FromHours(24);
    private const int BatchSize = 200;

    public UploadOrphanCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UploadOrphanCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[Upload Orphan Cleanup] Service started. Interval={Interval}h, grace={Grace}h",
            _interval.TotalHours, _gracePeriod.TotalHours);

        using var timer = new PeriodicTimer(_interval);

        // Initial pass on startup (catches anything that piled up during downtime).
        await SweepAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Upload Orphan Cleanup] Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Upload Orphan Cleanup] Unexpected error in sweep loop");
            }
        }
    }

    /// <summary>
    /// One sweep pass. Internal rather than private so the regression test can
    /// drive a single pass without running the timer loop.
    /// </summary>
    internal async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var s3 = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
            var cdn = scope.ServiceProvider.GetRequiredService<IOptions<CdnConfiguration>>().Value;

            // The grace period decides when a file is physically destroyed, so the
            // deadline is measured by the injected clock: a test can move that one,
            // the system clock it cannot.
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var cutoff = clock.Now - _gracePeriod;
            var candidates = await db.Uploads
                // Soft-deleted rows are exactly what this sweeper looks for, and
                // the global query filter hides them: without IgnoreQueryFilters
                // the predicate becomes "NOT IsRemoved AND IsRemoved" and no row
                // can ever match, so nothing is ever deleted from S3.
                .IgnoreQueryFilters()
                .Where(u => u.IsRemoved && u.DeletedUtc != null && u.DeletedUtc < cutoff)
                .OrderBy(u => u.DeletedUtc)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogDebug("[Upload Orphan Cleanup] No orphans to remove");
                return;
            }

            var deletedDbRows = 0;
            var deletedS3Objects = 0;

            foreach (var upload in candidates)
            {
                var allS3Removed = true;

                foreach (var key in EnumerateKeys(upload.ObjectKey))
                {
                    try
                    {
                        await s3.DeleteObjectAsync(new DeleteObjectRequest
                        {
                            BucketName = cdn.BucketName,
                            Key = key,
                        }, ct);
                        deletedS3Objects++;
                    }
                    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // The object is already deleted — normal for an idempotent retry.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[Upload Orphan Cleanup] Failed to delete S3 object {Key} (will retry next tick)",
                            key);
                        allS3Removed = false;
                    }
                }

                if (allS3Removed)
                {
                    db.Uploads.Remove(upload);
                    deletedDbRows++;
                }
            }

            if (deletedDbRows > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[Upload Orphan Cleanup] Swept {Candidates} candidate(s): deleted {S3Count} S3 objects + {DbCount} DB rows",
                candidates.Count, deletedS3Objects, deletedDbRows);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Upload Orphan Cleanup] Error during sweep");
        }
    }

    /// <summary>
    /// Single source file per upload (thumbnails live only in the imgproxy
    /// cache, not in S3 storage).
    /// </summary>
    private static System.Collections.Generic.IEnumerable<string> EnumerateKeys(string objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            yield break;
        }
        yield return objectKey;
    }
}
