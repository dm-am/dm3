using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Uploads;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <inheritdoc />
internal class UploadOrphanCollector : IUploadOrphanCollector
{
    /// <summary>Rows per pass, so one sweep cannot hold the store for minutes.</summary>
    private const int BatchSize = 200;

    private readonly DmDbContext _dbContext;
    private readonly IObjectStorage _objectStorage;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc cref="UploadOrphanCollector" />
    public UploadOrphanCollector(
        DmDbContext dbContext,
        IObjectStorage objectStorage,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _objectStorage = objectStorage;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<UploadSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        // The grace period decides when a file is physically destroyed, so the
        // deadline is measured by the injected clock: a test can move that one,
        // the system clock it cannot.
        var cutoff = _dateTimeProvider.Now - UploadPolicy.OrphanGracePeriod;

        var candidates = await _dbContext.Uploads
            // Soft-deleted rows are exactly what this sweeper looks for, and
            // the global query filter hides them: without IgnoreQueryFilters
            // the predicate becomes "NOT IsRemoved AND IsRemoved" and no row
            // can ever match, so nothing is ever deleted from S3.
            .IgnoreQueryFilters()
            .TagWith("DM.Uploads.SweepOrphans")
            .Where(u => u.IsRemoved && u.DeletedUtc != null && u.DeletedUtc < cutoff)
            .OrderBy(u => u.DeletedUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new UploadSweepResult(0, 0, 0);
        }

        var deletedRows = 0;
        var removedObjects = 0;

        foreach (var upload in candidates)
        {
            var allObjectsRemoved = true;

            foreach (var key in EnumerateKeys(upload.ObjectKey))
            {
                // The store answers whether the key holds anything now, and it
                // owns both the "already gone is fine" rule and the log line
                // for a refusal. What is left here is the decision only this
                // sweeper can make: keep the row for the next pass.
                if (await _objectStorage.DeleteAsync(key, cancellationToken))
                {
                    removedObjects++;
                }
                else
                {
                    allObjectsRemoved = false;
                }
            }

            if (allObjectsRemoved)
            {
                _dbContext.Uploads.Remove(upload);
                deletedRows++;
            }
        }

        if (deletedRows > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UploadSweepResult(candidates.Count, removedObjects, deletedRows);
    }

    /// <summary>
    /// Single source file per upload (thumbnails live only in the imgproxy
    /// cache, not in S3 storage).
    /// </summary>
    private static IEnumerable<string> EnumerateKeys(string objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            yield break;
        }

        yield return objectKey;
    }
}
