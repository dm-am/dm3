using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Tracing;
using Microsoft.AspNetCore.Http;
using UploadDto = DM.Web.API.Shared.Dto.Upload;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// Taking a file in: the ceiling of its type, the target and the right to it, the
/// room left on a post, the image pipeline, the object key, the PUT and the row
/// that names it, and the replay of a retried request with its metrics
/// </summary>
public partial class UploadApiService
{
    /// <summary>Max upload size — 10 MB (in sync with RequestSizeLimit on the controller).</summary>
    private const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The client sends a unique key per logical upload; a key that arrives twice
    /// gets the cached response instead of a second upload.
    /// </summary>
    internal const string IdempotencyHeader = "Idempotency-Key";
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Upload file directly with server-side processing.
    /// For images — magic-byte validation, EXIF strip, WebP thumbnail generation,
    /// atomic batch S3 PUT, extension normalization by the validated content-type.
    /// </summary>
    public async Task<UploadDto> DirectUpload(IFormFile file, UploadType type, Guid? targetId, UploadAudience audience)
    {
        using var activity = DmActivitySource.Source.StartActivity(
            "upload.direct",
            ActivityKind.Server);
        activity?.SetTag("upload.type", type.ToString());
        activity?.SetTag("upload.declared_content_type", file.ContentType);
        activity?.SetTag("upload.input_size_bytes", file.Length);

        // A retry with the same Idempotency-Key gets the cached response, per user.
        var userId = _identityProvider.Current.User.UserId;
        var idempotencyKey = _httpContext.HttpContext?.Request.Headers[IdempotencyHeader].ToString();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            activity?.SetTag("upload.idempotency_key", idempotencyKey);
            var cacheKey = $"upload:idemp:{userId:N}:{idempotencyKey}";
            return await _cache.GetOrCreateAsync(
                cacheKey,
                () => DirectUploadInstrumented(file, type, targetId, audience, activity),
                IdempotencyTtl);
        }

        return await DirectUploadInstrumented(file, type, targetId, audience, activity);
    }

    private async Task<UploadDto> DirectUploadInstrumented(
        IFormFile file,
        UploadType type,
        Guid? targetId,
        UploadAudience audience,
        Activity? activity)
    {
        var stopwatch = Stopwatch.StartNew();
        var typeTag = new KeyValuePair<string, object?>("type", type.ToString());

        try
        {
            var result = await DirectUploadCore(file, type, targetId, audience);

            stopwatch.Stop();
            UploadMetrics.Success.Add(1, typeTag,
                new("content_type", result.ContentType));
            UploadMetrics.Duration.Record(stopwatch.Elapsed.TotalSeconds, typeTag);
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
        catch (StorageException)
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

    private async Task<UploadDto> DirectUploadCore(IFormFile file, UploadType type, Guid? targetId, UploadAudience audience)
    {
        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        RequireSize(file, type);

        // 1, 1a and 1b. The target, the room left on it and who sees the file there, ahead of processing.
        var (target, addressees) = await ResolveTargetAsync(type, targetId, audience, userId);

        // 2. Buffer, validate and process in memory, the thumbnails of an avatar
        //    included.
        ProcessedImage processed;
        await using (var fileStream = file.OpenReadStream())
        {
            processed = await _imageProcessingService.ProcessAsync(fileStream, file.ContentType, type);
        }

        // 3. Generate the object key (the extension is NORMALIZED from the validated
        //    content-type, NOT from the user filename — anti-extension-spoofing).
        var objectKey = GenerateObjectKey(type, userId, processed.Extension);

        // 4. The original into the object store. Everything after it that can
        //    fail is wrapped in the rollback below.
        await _objectStorage.PutAsync(objectKey, processed.Bytes, processed.ContentType);

        Activity.Current?.SetTag("upload.output_size_bytes", processed.Bytes.LongLength);

        // 5. The thumbnails beside the original, then the DB record. If either
        //    fails — roll back the S3 PUTs.
        var newUpload = new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            TargetId = target,
            // Filename is NORMALIZED: the extension comes from the content-type, the original name
            // (if provided) is for UI display purposes only.
            FileName = SanitizeFileName(file.FileName, processed.Extension),
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            // Measured by the pipeline that produced these bytes, so the row and
            // the object in the bucket cannot disagree about the aspect ratio.
            Width = processed.Width,
            Height = processed.Height,
            ObjectKey = objectKey,
            Original = true,
            // Only for the types the bucket answers anonymously: elsewhere the link would 403.
            Url = UploadFolder.AnonymouslyReadable.Contains(type)
                ? _objectStorage.BuildPublicUrl(objectKey)
                : null,
            CreatedUtc = now,
            ConfirmedUtc = now,
            AddresseesJson = addressees,
        };

        StoredUpload stored;
        try
        {
            foreach (var thumbnail in processed.Thumbnails)
            {
                await _objectStorage.PutAsync(
                    AvatarThumbnail.AddressOf(objectKey, thumbnail.Size), thumbnail.Bytes, AvatarThumbnail.ContentType);
            }
            stored = await _uploadRepository.AddAsync(newUpload);
        }
        catch
        {
            // Compensation for the PUTs above: the orphan sweeper walks rows, and no
            // row names these objects. A thumbnail that never went in counts as removed.
            foreach (var key in AvatarThumbnail.ObjectKeysOf(objectKey, type))
            {
                await _objectStorage.DeleteAsync(key);
            }
            throw;
        }

        return MapToDto(stored, canOpen: true);
    }

    /// <summary>
    /// Refuses a file over the ceiling of its type, and an empty one.
    /// </summary>
    private static void RequireSize(IFormFile file, UploadType type)
    {
        // Before any buffering, and per type: the controller ceiling is one number
        // for the request, and an attachment is capped lower.
        var maxSizeBytes = MaxSizeFor(type);
        if (file.Length > maxSizeBytes)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"Максимальный размер: {maxSizeBytes / (1024 * 1024)} МБ",
            });
        }

        if (file.Length <= 0)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = "Пустой файл",
            });
        }
    }

    /// <summary>
    /// The entity the file is going to hang on, checked for being named, for the
    /// caller's right to it and for the room left on it, and the mark of who sees
    /// the file there.
    /// </summary>
    /// <remarks>
    /// The uploader may attach to the target, and so reads what he attaches: the
    /// answer opens the file to him.
    /// </remarks>
    private async Task<(Guid Target, string? Addressees)> ResolveTargetAsync(
        UploadType type, Guid? targetId, UploadAudience audience, Guid userId)
    {
        if (audience.VisibleTo != UploadVisibleTo.All && type != UploadType.PostAttachment)
        {
            throw AudienceOnlyForPostAttachment();
        }

        // 1. The target first, ahead of processing: a caller with no right to it
        // must not spend the pipeline. An unset target of a UserAvatar is the uploader.
        var effectiveTarget = targetId ?? (type == UploadType.UserAvatar ? userId : (Guid?)null);
        RequireTarget(type, effectiveTarget);
        // The module that owns the entity decides (IUploadTargetAuthorizer), failing closed.
        var authorizer = AuthorizerFor(type);
        await authorizer.EnsureAllowedAsync(effectiveTarget!.Value);

        // 1a. The files the target already carries: here, because the post is written
        // before its files. A race may put one extra file on a post; not worth an index.
        await EnsureTargetHasRoomAsync(type, effectiveTarget.Value);

        // 1b. Who sees the file, frozen against the target before anything is stored and
        // written in the insert of the row: the file is never visible wider than chosen.
        return (effectiveTarget.Value, await authorizer.FreezeAudienceAsync(effectiveTarget.Value, null, audience));
    }

    /// <summary>
    /// Rejects an upload whose target is missing. Every type points at exactly one
    /// entity, and a request that names none cannot be stored — this answers 400
    /// instead of letting the DB CHECK constraint answer 500.
    /// </summary>
    private static void RequireTarget(UploadType type, Guid? target)
    {
        var requirement = type switch
        {
            UploadType.UserAvatar => "Не указан пользователь",
            UploadType.CharacterAvatar => "Не указан персонаж",
            UploadType.PostAttachment => "Не указан пост",
            _ => throw new InvalidOperationException($"Unknown UploadType {type}"),
        };

        if (target == null)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["targetId"] = requirement,
            });
        }
    }

    /// <summary>
    /// The largest file this upload type accepts.
    /// </summary>
    private static long MaxSizeFor(UploadType type) => type switch
    {
        UploadType.PostAttachment => UploadPolicy.MaxPostAttachmentSizeBytes,
        _ => MaxUploadSizeBytes,
    };

    /// <summary>
    /// Refuses an upload the target has no room for.
    /// </summary>
    /// <remarks>
    /// Only the multi-slot type has a count to check. A character portrait is
    /// single by construction — the insert retires the previous row — and an
    /// avatar the same, so neither has a number to be over.
    /// </remarks>
    private async Task EnsureTargetHasRoomAsync(UploadType type, Guid target)
    {
        if (type != UploadType.PostAttachment)
        {
            return;
        }

        var attached = await _uploadRepository.CountPostAttachmentsAsync(target);
        if (attached >= UploadPolicy.MaxPostAttachments)
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["file"] = $"К посту можно приложить не больше {UploadPolicy.MaxPostAttachments} файлов",
            });
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

    /// <summary>
    /// Object key: type folder, scope (userId) and a whole random identifier, never
    /// reused or rewritten, which the immutable cache headers rest on. Not access
    /// control: the bucket answers anonymously on the public prefixes only.
    /// </summary>
    private static string GenerateObjectKey(UploadType type, Guid userId, string normalizedExtension)
    {
        // The prefix is not chosen here: the bucket policy grants anonymous reads
        // per prefix, so the two have to say the same thing about a type.
        var folder = UploadFolder.For(type);

        var uniqueId = Guid.NewGuid().ToString("N");
        var keyName = $"{userId:N}_{uniqueId}{normalizedExtension}";

        return $"{folder}/{keyName}";
    }
}
