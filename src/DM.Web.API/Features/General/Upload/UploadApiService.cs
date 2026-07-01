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

    /// <summary>Max upload size — 10 MB (sync с RequestSizeLimit на контроллере).</summary>
    private const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Idempotency-Key header. Клиент шлет уникальный per-логический-upload
    /// ключ; если тот же ключ приходит дважды — возвращаем cached response,
    /// не процессим повторно. TTL 1ч.
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

        // Admin scope=all → все uploads системы.
        if (all)
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListAll);
            return await GetUploadsInternal(query, userId: null);
        }

        // Admin username=... → uploads конкретного пользователя.
        if (!string.IsNullOrWhiteSpace(username))
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListUser);
            var user = await _userService.GetAsync(username);
            return await GetUploadsInternal(query, user.UserId);
        }

        // Default → собственные uploads.
        return await GetUploadsInternal(query, currentUserId);
    }

    /// <inheritdoc />
    public async Task<Shared.Dto.Upload> GetUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _dbContext.Uploads
            .Where(u => u.UploadId == id)
            .FirstOrDefaultAsync();

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Upload not found");
        }

        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Admin)
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

        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Admin)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        upload.IsRemoved = true;
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

        // Idempotency: если клиент шлет Idempotency-Key, возвращаем cached
        // response для дублирующих retry'ев (mobile сетевые retry, double-
        // click ниже rate-limit окна). Cache scoped per-user — кросс-юзер
        // replay невозможен.
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
            // OutputSizeBytes пишется внутри DirectUploadCore через activity tag —
            // здесь добавим, если есть.
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

        // Размер проверяем заранее (до magic-byte). Это дешевая проверка
        // на DoS-вектор — не пускаем 10+ MB в buffer / процессинг.
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
            // Не-image upload'ы (PostAttachment): простой single-PUT без процессинга.
            return await UploadNonImageAsync(file, type, targetId, userId, now);
        }

        // 1. Buffer + validate + process (in-memory; magic-byte, EXIF strip,
        //    decompression-bomb guard, downscale до 1024 px). Один файл —
        //    thumbnails генерируются on-the-fly через imgproxy при serving.
        ProcessedImage processed;
        await using (var fileStream = file.OpenReadStream())
        {
            processed = await _imageProcessingService.ProcessAsync(fileStream, file.ContentType);
        }

        // 2. Генерируем object key (расширение НОРМАЛИЗОВАНО из validated
        //    content-type, НЕ из user-filename — anti-extension-spoofing).
        var objectKey = GenerateObjectKey(type, userId, processed.Extension);

        // 3. Один S3 PUT (никаких batch + rollback list — один шаг,
        //    либо успех, либо failure → следующий блок сделает rollback).
        try
        {
            await PutToS3Async(objectKey, processed.Bytes, processed.ContentType);
        }
        catch
        {
            // Ничего PUT'ить было не успели — просто пробрасываем.
            throw;
        }

        Activity.Current?.SetTag("upload.output_size_bytes", processed.Bytes.LongLength);

        // 4. DB-запись. Если SaveChangesAsync упадет — rollback S3 PUT.
        // Effective target: для UserAvatar когда targetId не задан явно,
        // owner uploader = self (грузим свой аватар). Для CharacterAvatar
        // и PostAttachment targetId обязателен.
        var effectiveTarget = targetId ?? (type == UploadType.UserAvatar ? userId : (Guid?)null);
        var upload = new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            // Filename НОРМАЛИЗОВАН: расширение из content-type, original-имя
            // (если пришло) только для UI display purposes.
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
    /// Заполняет одну из TargetUserId / TargetCharacterId / TargetPostId
    /// в зависимости от <paramref name="type"/>. Кидает <see cref="HttpBadRequestException"/>
    /// если target обязателен, но не передан.
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
            // objectKey hash-based (immutable) → агрессивное browser/CDN кеширование.
            // Заменяем аватар = новый ключ, никаких cache-busting issues.
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
                // Best-effort rollback — оставшиеся orphans подметет фоновый GC.
            }
        }
    }

    private static string SanitizeFileName(string? originalName, string normalizedExtension)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            return $"image{normalizedExtension}";
        }
        // Сохраняем base-name (для UX в Uploads-tab), расширение всегда normalized.
        var baseName = Path.GetFileNameWithoutExtension(originalName);
        // Убираем все символы кроме букв/цифр/тире/подчеркивания (anti-path-traversal).
        var safe = Regex.Replace(baseName, @"[^\p{L}\p{N}_\-.]", "_");
        if (safe.Length > 80) safe = safe[..80];
        return $"{safe}{normalizedExtension}";
    }

    private async Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploadsInternal(
        UploadsQuery query, Guid? userId)
    {
        var queryable = _dbContext.Uploads.AsQueryable();

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
    /// Hash-based immutable object key: тип-папка + scope (userId) + 8-char hex.
    /// Расширение принимаем как валидированную нормализованную строку.
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
            Type = upload.Type,
            // Дедуцируем TargetId из соответствующей типизированной колонки.
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
