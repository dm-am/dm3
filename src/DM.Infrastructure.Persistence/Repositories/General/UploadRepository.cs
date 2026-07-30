using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using Microsoft.EntityFrameworkCore;
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
    public async Task SoftDeleteAsync(Guid uploadId, DateTimeOffset deletedUtc)
    {
        var upload = await _dbContext.Uploads.FirstOrDefaultAsync(u => u.UploadId == uploadId);
        if (upload == null)
        {
            // Someone else hid the row between the caller's check and this call:
            // the requested end state already holds, so there is nothing to undo
            // and nothing to report.
            return;
        }

        upload.IsRemoved = true;
        // DeletedUtc is what starts the grace period the orphan sweeper waits
        // out before deleting the object from S3; without it the row is hidden
        // from the site but the file stays in the bucket forever.
        upload.DeletedUtc = deletedUtc;
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
            ObjectKey = upload.ObjectKey,
            FilePath = upload.Url,
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc,
        };
        AssignTypedTarget(entity, upload.Type, upload.TargetId);

        try
        {
            await _dbContext.Uploads.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // The caller has an object in the bucket that this row was supposed
            // to point at. Translate here so it can recognise a refused write
            // without depending on EF.
            throw new StorageException("Failed to store the upload record", ex);
        }

        // Owner is not resolved on this path — the caller already knows whose
        // file it is, and a lookup purely to echo the username back would cost
        // a query per upload.
        return ToStored(entity);
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
