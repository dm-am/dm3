using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <inheritdoc />
internal class UploadGarbageCollector : IUploadGarbageCollector
{
    private readonly DmDbContext _db;
    private readonly IAmazonS3 _s3;
    private readonly CdnConfiguration _cdn;
    private readonly ILogger<UploadGarbageCollector> _logger;

    /// <inheritdoc />
    public UploadGarbageCollector(
        DmDbContext db,
        IAmazonS3 s3,
        IOptions<CdnConfiguration> cdn,
        ILogger<UploadGarbageCollector> logger)
    {
        _db = db;
        _s3 = s3;
        _cdn = cdn.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CollectObsoleteAsync(Guid entityId)
    {
        // Take all active uploads of the entity, sorted by date;
        // the most recent (HEAD) is the current avatar, the rest are obsolete.
        // The filter covers all typed target columns: if
        // entityId is a user, we find UserAvatar; if a character — CharacterAvatar.
        var uploads = await _db.Uploads
            .Where(u => !u.IsRemoved &&
                (u.TargetUserId == entityId
                    || u.TargetCharacterId == entityId
                    || u.TargetPostId == entityId))
            .OrderByDescending(u => u.CreatedUtc)
            .ToListAsync();

        if (uploads.Count <= 1)
        {
            return;
        }

        var obsolete = uploads.Skip(1).ToList();
        var s3Keys = new List<string>();
        foreach (var up in obsolete)
        {
            // One file per upload (imgproxy makes thumbnails on-the-fly,
            // no pre-generated _m/_s files).
            if (!string.IsNullOrEmpty(up.ObjectKey))
            {
                s3Keys.Add(up.ObjectKey);
            }

            up.IsRemoved = true;
            up.DeletedUtc = DateTimeOffset.UtcNow;
        }

        // Persist the soft-delete in the DB before touching S3 — if the S3
        // delete fails, the DB is still consistent (the orphan file is swept
        // by the background worker later).
        await _db.SaveChangesAsync();

        foreach (var key in s3Keys)
        {
            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _cdn.BucketName,
                    Key = key,
                });
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The file is already deleted or does not exist — that is fine.
            }
            catch (Exception ex)
            {
                // Log but do not fail — the corresponding Upload is marked
                // IsRemoved, the GC worker picks the file up on the next pass.
                _logger.LogWarning(ex, "Failed to delete obsolete S3 object {Key}", key);
            }
        }
    }
}
