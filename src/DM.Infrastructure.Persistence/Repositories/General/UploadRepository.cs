using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <inheritdoc />
internal class UploadRepository : IUploadRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public UploadRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyCollection<StoredUpload> Uploads, int TotalCount)> GetPageAsync(
        UploadFilter filter, int skip, int take)
    {
        // Owner is loaded so the moderation "Загрузил" column can render the
        // uploader username/profile link for every file (doc 4.2.3.8.9).
        // Include, not a projection: on a required navigation a projected join
        // would drop upload rows whose owner the soft-delete filter hides,
        // silently shrinking the moderation list.
        var queryable = _dbContext.Uploads.Include(u => u.Owner).AsQueryable();

        if (filter.UserId.HasValue)
        {
            queryable = queryable.Where(u => u.UserId == filter.UserId.Value);
        }

        if (filter.Type.HasValue)
        {
            queryable = queryable.Where(u => u.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            queryable = queryable.Where(u => u.Status == filter.Status.Value);
        }

        var totalCount = await queryable.CountAsync();

        var uploads = await queryable
            .OrderByDescending(u => u.CreatedUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (uploads.Select(ToStored).ToList(), totalCount);
    }

    /// <inheritdoc />
    public async Task<StoredUpload?> GetAsync(Guid uploadId)
    {
        var upload = await _dbContext.Uploads
            .Include(u => u.Owner)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId);

        return upload == null ? null : ToStored(upload);
    }

    /// <inheritdoc />
    public Task<string?> GetObjectKeyAsync(Guid uploadId) => _dbContext.Uploads
        .Where(u => u.UploadId == uploadId)
        .Select(u => (string?)u.ObjectKey)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task<int> CountPostAttachmentsAsync(Guid postId) => _dbContext.Uploads
        .CountAsync(u => u.TargetPostId == postId && u.Type == UploadType.PostAttachment);

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid uploadId, Guid? deletedByUserId, DateTimeOffset deletedUtc)
    {
        var upload = await _dbContext.Uploads.FirstOrDefaultAsync(u => u.UploadId == uploadId);
        if (upload == null)
        {
            // Someone else hid the row between the caller's check and this call:
            // the requested end state already holds, so there is nothing to undo
            // and nothing to report.
            return;
        }

        // DeletedUtc is what starts the grace period the orphan sweeper waits
        // out before deleting the object from S3; without it the row is hidden
        // from the site but the file stays in the bucket forever. The author
        // travels with it: deleting somebody else's file is a moderation action,
        // and the column that would answer who did it was left empty here while
        // the request had the identity in hand.
        SoftDelete.Mark(upload, deletedByUserId, deletedUtc);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<StoredUpload> AddAsync(NewUpload upload)
    {
        var entity = new DbUpload
        {
            UploadId = upload.Id,
            UserId = upload.UserId,
            Type = upload.Type,
            Status = upload.Status,
            Original = upload.Original,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            SizeBytes = upload.SizeBytes,
            Width = upload.Width,
            Height = upload.Height,
            ObjectKey = upload.ObjectKey,
            FilePath = upload.Url,
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc,
        };
        AssignTypedTarget(entity, upload.Type, upload.TargetId);

        try
        {
            if (upload.Type == UploadType.CharacterAvatar)
            {
                await ReplaceCharacterPortraitAsync(entity);
            }
            else
            {
                await _dbContext.Uploads.AddAsync(entity);
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            // The caller has an object in the bucket that this row was supposed
            // to point at. Translate here so it can recognise a refused write
            // without depending on EF. Both shapes, because the replacement path
            // retires the previous row with ExecuteUpdate, which reports a refusal
            // as the provider exception instead of wrapping it.
            throw new StorageException("Failed to store the upload record", ex);
        }

        // Owner is not resolved on this path — the caller already knows whose
        // file it is, and a lookup purely to echo the username back would cost
        // a query per upload.
        return ToStored(entity);
    }

    /// <summary>
    /// Inserts a character portrait and retires the one it replaces, both in the
    /// same transaction.
    /// </summary>
    /// <remarks>
    /// A character carries no column pointing at its portrait: the portrait is
    /// whichever live CharacterAvatar row points at the character, so a second one
    /// is not an extra picture but a second answer to one question. The schema
    /// holds that rule now (unique partial index over the live portraits), and the
    /// rule decides the order here: the previous row has to go before the new one
    /// lands, not be collected after it, or the index refuses the insert and a
    /// legitimate replacement answers 500. Together, because retiring on its own
    /// would answer a refused insert by leaving the character with no portrait.
    ///
    /// The file behind the retired row stays in the bucket until the orphan
    /// sweeper is done waiting out its grace period, which is the recovery window
    /// every other soft-deleted upload gets.
    /// </remarks>
    /// <param name="entity">Row of the new portrait, target column already assigned.</param>
    private async Task ReplaceCharacterPortraitAsync(DbUpload entity)
    {
        // The strategy wrapper is required because the API host configures
        // EnableRetryOnFailure.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // The soft-delete filter keeps this to the live rows, so a portrait
            // retired earlier does not get its grace period restarted. The stamp is
            // the new upload's own moment: that is when the old one was superseded.
            await _dbContext.Uploads
                .Where(u => u.TargetCharacterId == entity.TargetCharacterId
                    && u.Type == UploadType.CharacterAvatar)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.IsRemoved, true)
                    .SetProperty(x => x.DeletedUtc, (DateTimeOffset?)entity.CreatedUtc));

            _dbContext.Uploads.Add(entity);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// Routes the single logical target into the typed column the upload type
    /// owns. The columns are mutually exclusive and a DB CHECK constraint pairs
    /// each one with its type, so the mapping is not the caller's business.
    /// </summary>
    private static void AssignTypedTarget(DbUpload upload, UploadType type, Guid? target)
    {
        switch (type)
        {
            case UploadType.UserAvatar:
                upload.TargetUserId = target;
                break;
            case UploadType.CharacterAvatar:
                upload.TargetCharacterId = target;
                break;
            case UploadType.PostAttachment:
                upload.TargetPostId = target;
                break;
            default:
                throw new InvalidOperationException($"Unknown UploadType {type}");
        }
    }

    private static StoredUpload ToStored(DbUpload upload) => new()
    {
        Id = upload.UploadId,
        UserId = upload.UserId,
        OwnerUsername = upload.Owner?.Username,
        Type = upload.Type,
        Status = upload.Status,
        // Deduce the target from the corresponding typed column.
        TargetId = upload.TargetUserId ?? upload.TargetCharacterId ?? upload.TargetPostId,
        FileName = upload.FileName ?? string.Empty,
        ContentType = upload.ContentType ?? string.Empty,
        SizeBytes = upload.SizeBytes,
        Url = upload.FilePath,
        CreatedUtc = upload.CreatedUtc,
        ConfirmedUtc = upload.ConfirmedUtc,
    };
}
