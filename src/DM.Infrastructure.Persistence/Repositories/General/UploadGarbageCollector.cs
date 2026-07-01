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
        // Берем все активные uploads сущности, отсортированные по дате;
        // самый свежий (HEAD) — текущий аватар, остальные obsolete.
        // Filter охватывает все типизированные target-колонки: если
        // entityId — user, найдем UserAvatar; если character — CharacterAvatar.
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
            // Один файл per upload (imgproxy делает thumbnails on-the-fly,
            // никаких пре-сгенерированных _m/_s файлов).
            if (!string.IsNullOrEmpty(up.ObjectKey))
            {
                s3Keys.Add(up.ObjectKey);
            }

            up.IsRemoved = true;
            up.DeletedUtc = DateTimeOffset.UtcNow;
        }

        // Сохраняем soft-delete в БД до того, как трогаем S3 — если S3
        // delete упадет, DB все равно консистентна (orphan-файл подметет
        // фоновый worker позже).
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
                // Файл уже удален или не существует — норма.
            }
            catch (Exception ex)
            {
                // Логируем, но не падаем — соответствующий Upload помечен
                // IsRemoved, GC-worker заберет файл при следующем проходе.
                _logger.LogWarning(ex, "Failed to delete obsolete S3 object {Key}", key);
            }
        }
    }
}
