using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <inheritdoc />
internal class UploadOrphanCollector : IUploadOrphanCollector
{
    // Rows per pass, so one sweep cannot hold the store for minutes.
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
            .Where(u =>
                (u.IsRemoved && u.DeletedUtc != null && u.DeletedUtc < cutoff &&
                 // An avatar a warning on a profile pinned is the evidence of that
                 // warning, and the owner replacing his avatar marks it deleted with no
                 // author: such a row stays, and so does one the owner deleted himself.
                 // A deletion by anybody else is moderation's decision about the
                 // picture, and the row goes as any other; the key then clears the
                 // warning's column (DATA_STORAGE.md, "Объектное хранилище").
                 ((u.DeletedByUserId != null && u.DeletedByUserId != u.UserId) ||
                  !_dbContext.Warnings.Any(w => w.ProfileAvatarUploadId == u.UploadId))) ||
                // An avatar uploaded and never saved to the profile, the tab closed
                // before "Сохранить": nothing marks it deleted, so its age past the
                // grace period is what makes it an orphan. A removed account keeps
                // the avatar it points at.
                (!u.IsRemoved && u.Type == UploadType.UserAvatar && u.Status == UploadStatus.Confirmed &&
                 u.CreatedUtc < cutoff &&
                 !_dbContext.Users.IgnoreQueryFilters().Any(user => user.AvatarUploadId == u.UploadId)))
            .OrderBy(u => u.DeletedUtc ?? u.CreatedUtc)
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
            // The original and the thumbnails beside it. The store owns the "already
            // gone is fine" rule; a refusal keeps the row for the next pass.
            if (!string.IsNullOrEmpty(upload.ObjectKey))
            {
                var keys = AvatarThumbnail.ObjectKeysOf(upload.ObjectKey, upload.Type);
                var refused = false;
                foreach (var key in keys)
                {
                    refused |= !await _objectStorage.DeleteAsync(key, cancellationToken);
                }

                if (refused)
                {
                    continue;
                }

                removedObjects += keys.Count;
            }

            _dbContext.Uploads.Remove(upload);
            deletedRows++;
        }

        if (deletedRows > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UploadSweepResult(candidates.Count, removedObjects, deletedRows);
    }
}
