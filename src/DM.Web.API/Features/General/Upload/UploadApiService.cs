using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Persistence;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Web.API.Features.General.Upload;

/// <inheritdoc />
internal class UploadApiService : IUploadApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserService _userService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAmazonS3 _s3Client;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ICache _cache;
    private readonly IHttpContextAccessor _httpContext;
    private readonly CdnConfiguration _cdnConfig;

    /// <summary>Max upload size — 10 MB (in sync with RequestSizeLimit on the controller).</summary>
    private const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Idempotency-Key header. The client sends a unique per-logical-upload
    /// key; if the same key arrives twice we return the cached response
    /// instead of reprocessing. TTL 1h.
    /// </summary>
    private const string IdempotencyHeader = "Idempotency-Key";
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public UploadApiService(
        DmDbContext dbContext,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        IAmazonS3 s3Client,
        IImageProcessingService imageProcessingService,
        ICache cache,
        IHttpContextAccessor httpContext,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _dbContext = dbContext;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userService = userService;
        _dateTimeProvider = dateTimeProvider;
        _s3Client = s3Client;
        _imageProcessingService = imageProcessingService;
        _cache = cache;
        _httpContext = httpContext;
        _cdnConfig = cdnOptions.Value;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploads(
        UploadsQuery query, string? username, bool all)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // Admin scope=all → all uploads in the system.
        if (all)
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListAll);
            return await GetUploadsInternal(query, userId: null);
        }

        // Moderator+ username=... → uploads of a specific user.
        if (!string.IsNullOrWhiteSpace(username))
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListUser);
            var user = await _userService.GetAsync(username);
            return await GetUploadsInternal(query, user.UserId);
        }

        // Default → own uploads.
        return await GetUploadsInternal(query, currentUserId);
    }

    /// <inheritdoc />
    public async Task<Shared.Dto.Upload> GetUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _dbContext.Uploads
            .Include(u => u.Owner)
            .Where(u => u.UploadId == id)
            .FirstOrDefaultAsync();

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Upload not found");
        }

        // Owner self-view; viewing another user's file is a moderation action
        // (Moderator+), aligned with the list + delete endpoints.
        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Moderator)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        return MapToDto(upload);
    }

    /// <inheritdoc />
    public async Task DeleteUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _dbContext.Uploads
            .Where(u => u.UploadId == id)
            .FirstOrDefaultAsync();

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Upload not found");
        }

        // Owner self-service; deleting others' files is a moderation action (Moderator+).
        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Moderator)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        upload.IsRemoved = true;
        // DeletedUtc is what starts the grace period the orphan sweeper waits
        // out before deleting the object from S3; without it the row is hidden
        // from the site but the file stays in the bucket forever.
        upload.DeletedUtc = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Shared.Dto.Upload> DirectUpload(IFormFile file, UploadType type, Guid? targetId)
    {
        using var activity = DmActivitySource.Source.StartActivity(
            "upload.direct",
            ActivityKind.Server);
        activity?.SetTag("upload.type", type.ToString());
        activity?.SetTag("upload.declared_content_type", file.ContentType);
        activity?.SetTag("upload.input_size_bytes", file.Length);

        // Idempotency: if the client sends an Idempotency-Key, return the cached
        // response for duplicate retries (mobile network retries, double-
        // clicks below the rate-limit window). The cache is scoped per user —
        // cross-user replay is impossible.
        var userId = _identityProvider.Current.User.UserId;
        var idempotencyKey = _httpContext.HttpContext?.Request.Headers[IdempotencyHeader].ToString();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            activity?.SetTag("upload.idempotency_key", idempotencyKey);
            var cacheKey = $"upload:idemp:{userId:N}:{idempotencyKey}";
            return await _cache.GetOrCreateAsync(
                cacheKey,
                () => DirectUploadInstrumented(file, type, targetId, activity),
                IdempotencyTtl);
        }

        return await DirectUploadInstrumented(file, type, targetId, activity);
    }

    private async Task<Shared.Dto.Upload> DirectUploadInstrumented(
        IFormFile file,
        UploadType type,
        Guid? targetId,
        Activity? activity)
    {
        var stopwatch = Stopwatch.StartNew();
        var typeTag = new KeyValuePair<string, object?>("type", type.ToString());

        try
        {
            var result = await DirectUploadCore(file, type, targetId);

            stopwatch.Stop();
            UploadMetrics.Success.Add(1, typeTag,
                new("content_type", result.ContentType));
            UploadMetrics.DurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, typeTag);
            UploadMetrics.InputSizeBytes.Record(file.Length, typeTag);
            // OutputSizeBytes is written inside DirectUploadCore via an activity tag —
            // add it here if present.
            if (activity?.GetTagItem("upload.output_size_bytes") is long outputBytes)
            {
                UploadMetrics.OutputSizeBytes.Record(outputBytes, typeTag);
            }
            return result;
        }
        catch (HttpBadRequestException)
        {
            UploadMetrics.Failure.Add(1, typeTag, new("reason", "validation"));
            throw;
        }
        catch (AmazonS3Exception)
        {
            UploadMetrics.Failure.Add(1, typeTag, new("reason", "s3"));
            activity?.SetStatus(ActivityStatusCode.Error, "S3 failure");
            throw;
        }
        catch (DbUpdateException)
        {
            UploadMetrics.Failure.Add(1, typeTag, new("reason", "db"));
            activity?.SetStatus(ActivityStatusCode.Error, "DB failure");
            throw;
        }
        catch (Exception ex)
        {
            UploadMetrics.Failure.Add(1, typeTag, new("reason", "other"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<Shared.Dto.Upload> DirectUploadCore(IFormFile file, UploadType type, Guid? targetId)
    {
        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        // Check the size upfront (before magic bytes). A cheap check
        // against a DoS vector — do not let 10+ MB into the buffer / processing.
        if (file.Length > MaxUploadSizeBytes)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Максимальный размер: {MaxUploadSizeBytes / (1024 * 1024)} МБ",
            });
        }

        if (file.Length <= 0)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Пустой файл",
            });
        }

        if (!_imageProcessingService.IsImageType(type))
        {
            // Non-image uploads (PostAttachment): a simple single PUT without processing.
            return await UploadNonImageAsync(file, type, targetId, userId, now);
        }

        // 1. Buffer + validate + process (in-memory; magic-byte, EXIF strip,
        //    decompression-bomb guard, downscale to 1024 px). A single file —
        //    thumbnails are generated on-the-fly via imgproxy at serving time.
        ProcessedImage processed;
        await using (var fileStream = file.OpenReadStream())
        {
            processed = await _imageProcessingService.ProcessAsync(fileStream, file.ContentType);
        }

        // 2. Generate the object key (the extension is NORMALIZED from the validated
        //    content-type, NOT from the user filename — anti-extension-spoofing).
        var objectKey = GenerateObjectKey(type, userId, processed.Extension);

        // 3. A single S3 PUT (no batch + rollback list — one step,
        //    either success or failure → the next block does the rollback).
        try
        {
            await PutToS3Async(objectKey, processed.Bytes, processed.ContentType);
        }
        catch
        {
            // Nothing was PUT yet — just rethrow.
            throw;
        }

        Activity.Current?.SetTag("upload.output_size_bytes", processed.Bytes.LongLength);

        // 4. DB record. If SaveChangesAsync fails — roll back the S3 PUT.
        // Effective target: for UserAvatar, when targetId is not set explicitly,
        // the owner uploader = self (uploading one's own avatar). For CharacterAvatar
        // and PostAttachment, targetId is required.
        var effectiveTarget = targetId ?? (type == UploadType.UserAvatar ? userId : (Guid?)null);
        var upload = new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            // Filename is NORMALIZED: the extension comes from the content-type, the original name
            // (if provided) is for UI display purposes only.
            FileName = SanitizeFileName(file.FileName, processed.Extension),
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            ObjectKey = objectKey,
            Original = true,
            FilePath = GeneratePublicUrl(objectKey),
            CreatedUtc = now,
            ConfirmedUtc = now,
        };
        AssignTypedTarget(upload, type, effectiveTarget);

        try
        {
            await _dbContext.Uploads.AddAsync(upload);
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            await RollbackS3PutsAsync(new[] { objectKey });
            throw;
        }

        return MapToDto(upload);
    }

    private async Task<Shared.Dto.Upload> UploadNonImageAsync(
        IFormFile file, UploadType type, Guid? targetId, Guid userId, DateTimeOffset now)
    {
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        var objectKey = GenerateObjectKey(type, userId, extension);
        await using (var stream = file.OpenReadStream())
        {
            await PutToS3Async(objectKey, stream, file.ContentType);
        }

        var upload = new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            FileName = SanitizeFileName(file.FileName, extension),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            ObjectKey = objectKey,
            Original = true,
            FilePath = GeneratePublicUrl(objectKey),
            CreatedUtc = now,
            ConfirmedUtc = now,
        };
        AssignTypedTarget(upload, type, targetId);
        try
        {
            await _dbContext.Uploads.AddAsync(upload);
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            await RollbackS3PutsAsync(new[] { objectKey });
            throw;
        }
        return MapToDto(upload);
    }

    /// <summary>
    /// Fills one of TargetUserId / TargetCharacterId / TargetPostId
    /// depending on <paramref name="type"/>. Throws <see cref="HttpBadRequestException"/>
    /// if a target is required but not provided.
    /// </summary>
    private static void AssignTypedTarget(DbUpload upload, UploadType type, Guid? target)
    {
        switch (type)
        {
            case UploadType.UserAvatar:
                if (target == null)
                    throw new HttpBadRequestException(new Dictionary<string, string>
                    {
                        ["targetId"] = "User avatar requires a target user ID",
                    });
                upload.TargetUserId = target;
                break;
            case UploadType.CharacterAvatar:
                if (target == null)
                    throw new HttpBadRequestException(new Dictionary<string, string>
                    {
                        ["targetId"] = "Character avatar requires a target character ID",
                    });
                upload.TargetCharacterId = target;
                break;
            case UploadType.PostAttachment:
                if (target == null)
                    throw new HttpBadRequestException(new Dictionary<string, string>
                    {
                        ["targetId"] = "Post attachment requires a target post ID",
                    });
                upload.TargetPostId = target;
                break;
            default:
                throw new InvalidOperationException($"Unknown UploadType {type}");
        }
    }

    private async Task PutToS3Async(string objectKey, byte[] bytes, string contentType)
    {
        await PutToS3Async(objectKey, new MemoryStream(bytes, writable: false), contentType);
    }

    private async Task PutToS3Async(string objectKey, Stream stream, string contentType)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            // objectKey is hash-based (immutable) → aggressive browser/CDN caching.
            // Replacing the avatar = a new key, no cache-busting issues.
            Headers =
            {
                CacheControl = "public, max-age=31536000, immutable",
            },
        };
        await _s3Client.PutObjectAsync(putRequest);
    }

    private async Task RollbackS3PutsAsync(IReadOnlyCollection<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _cdnConfig.BucketName,
                    Key = key,
                });
            }
            catch
            {
                // Best-effort rollback — remaining orphans are swept by the background GC.
            }
        }
    }

    private static string SanitizeFileName(string? originalName, string normalizedExtension)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return $"image{normalizedExtension}";
        }
        // Keep the base name (for UX in the Uploads tab); the extension is always normalized.
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        // Strip all characters except letters/digits/dashes/underscores (anti-path-traversal).
        var safe = Regex.Replace(baseName, @"[^\p{L}\p{N}_\-.]", "_");
        if (safe.Length > 80) safe = safe[..80];
        return $"{safe}{normalizedExtension}";
    }

    private async Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploadsInternal(
        UploadsQuery query, Guid? userId)
    {
        // Owner is loaded so the moderation "Загрузил" column can render the
        // uploader username/profile link for every file (doc 4.2.3.8.9).
        var queryable = _dbContext.Uploads.Include(u => u.Owner).AsQueryable();

        if (userId.HasValue)
        {
            queryable = queryable.Where(u => u.UserId == userId.Value);
        }

        if (query.Type.HasValue)
        {
            queryable = queryable.Where(u => u.Type == query.Type.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(u => u.Status == query.Status.Value);
        }

        var totalCount = await queryable.CountAsync();

        var uploads = await queryable
            .OrderByDescending(u => u.CreatedUtc)
            .Skip((query.Number - 1) * query.Size)
            .Take(query.Size)
            .ToListAsync();

        var paging = new PagingInfo(PagingResult.Create(totalCount, query.Number, query.Size));
        return (uploads.Select(MapToDto), paging);
    }

    /// <summary>
    /// Hash-based immutable object key: type folder + scope (userId) + 8-char hex.
    /// The extension is accepted as a validated, normalized string.
    /// </summary>
    private string GenerateObjectKey(UploadType type, Guid userId, string normalizedExtension)
    {
        var folder = type switch
        {
            UploadType.UserAvatar => "avatars",
            UploadType.CharacterAvatar => "characters",
            UploadType.PostAttachment => "posts",
            _ => "misc",
        };

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var ext = string.IsNullOrEmpty(normalizedExtension) ? string.Empty : normalizedExtension;
        var keyName = $"{userId:N}_{uniqueId}{ext}";

        return string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{keyName}"
            : $"{_cdnConfig.Folder}/{folder}/{keyName}";
    }

    private string GeneratePublicUrl(string objectKey)
    {
        return new UriBuilder(new Uri(_cdnConfig.PublicUrl))
        {
            Path = $"{_cdnConfig.BucketName}/{objectKey}",
        }.ToString();
    }

    private static Shared.Dto.Upload MapToDto(DbUpload upload)
    {
        return new Shared.Dto.Upload
        {
            Id = upload.UploadId,
            UserId = upload.UserId,
            // Only populated when the Owner navigation was Include()d (the
            // moderation list queries); null on the DirectUpload/self paths.
            UploaderUsername = upload.Owner?.Username,
            Type = upload.Type,
            // Deduce TargetId from the corresponding typed column.
            TargetId = upload.TargetUserId ?? upload.TargetCharacterId ?? upload.TargetPostId,
            OriginalFileName = upload.FileName ?? string.Empty,
            ContentType = upload.ContentType ?? string.Empty,
            SizeBytes = upload.SizeBytes,
            Status = upload.Status,
            Url = upload.FilePath,
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc,
        };
    }
}
