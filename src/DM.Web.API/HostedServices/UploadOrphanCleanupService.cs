using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.HostedServices;

/// <summary>
/// Background worker, физически удаляющий S3-объекты для soft-deleted uploads.
///
/// Workflow:
/// 1. Periodically (раз в N часов) выбирает Uploads.IsRemoved=true, у которых
///    DeletedUtc старше grace-period (24ч по умолчанию).
/// 2. Для каждой такой записи удаляет оригинал + thumbnails (_m.webp / _s.webp)
///    в S3. Не падает на 404 (файла нет — норма) или transient errors.
/// 3. Удаляет саму запись Upload из БД (hard-delete) только после успешного
///    удаления всех S3-объектов. Если S3-delete упал — оставляем запись, ретрай
///    на следующем тике.
///
/// Grace period нужен на случай short-term восстановления: юзер ткнул «удалить
/// аватар», передумал — restore работает в первые 24ч.
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

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var s3 = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
            var cdn = scope.ServiceProvider.GetRequiredService<IOptions<CdnConfiguration>>().Value;

            var cutoff = DateTimeOffset.UtcNow - _gracePeriod;
            var candidates = await db.Uploads
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
                        // Object уже удален — норма для idempotent retry.
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
    /// Single source-file per upload (thumbnails живут только в imgproxy
    /// cache, не в S3 хранилище).
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
