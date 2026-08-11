using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
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
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.General.Upload;

/// <inheritdoc />
internal class UploadApiService : IUploadApiService
{
    private readonly IUploadRepository _uploadRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserService _userService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IObjectStorage _objectStorage;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ICache _cache;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IReadOnlyCollection<IUploadTargetAuthorizer> _targetAuthorizers;
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
        IUploadRepository uploadRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        IObjectStorage objectStorage,
        IImageProcessingService imageProcessingService,
        ICache cache,
        IHttpContextAccessor httpContext,
        IEnumerable<IUploadTargetAuthorizer> targetAuthorizers,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _uploadRepository = uploadRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userService = userService;
        _dateTimeProvider = dateTimeProvider;
        _objectStorage = objectStorage;
        _imageProcessingService = imageProcessingService;
        _cache = cache;
        _httpContext = httpContext;
        _targetAuthorizers = targetAuthorizers.ToList();
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
        var upload = await _uploadRepository.GetAsync(id);

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, RefusalMessage.UploadNotFound);
        }

        // Owner self-view; viewing another user's file is a moderation action
        // (Moderator+), aligned with the list + delete endpoints. Asked of the
        // intention that names the rule: it used to be spelled out here and again
        // in DeleteUpload, while UploadIntention.View resolved to nothing.
        _intentionManager.ThrowIfForbidden(UploadIntention.View, upload);

        return MapToDto(upload);
    }

    /// <inheritdoc />
    public async Task DeleteUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _uploadRepository.GetAsync(id);

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, RefusalMessage.UploadNotFound);
        }

        // Owner self-service; deleting others' files is a moderation action (Moderator+).
        _intentionManager.ThrowIfForbidden(UploadIntention.Delete, upload);

        await _uploadRepository.SoftDeleteAsync(id, userId, _dateTimeProvider.Now);
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

        // 1. Resolve and check the target before anything else. For UserAvatar an
        // unset targetId means the uploader themselves; for CharacterAvatar and
        // PostAttachment it is required. The null check used to sit after the PUT
        // and outside the rollback, so a valid image with a missing targetId left
        // an object in the bucket that nothing would ever collect: the orphan
        // sweeper walks rows, and the failed request never wrote one.
        //
        // Ahead of processing, not merely ahead of the PUT: decoding, EXIF
        // stripping and downscaling up to 10 MB is the expensive part of this
        // request, and a caller with no right to the target should not be able to
        // spend it.
        var effectiveTarget = targetId ?? (type == UploadType.UserAvatar ? userId : (Guid?)null);
        RequireTarget(type, effectiveTarget);
        await AuthorizeTargetAsync(type, effectiveTarget!.Value);

        // 2. Buffer + validate + process (in-memory; magic-byte, EXIF strip,
        //    decompression-bomb guard, downscale to 1024 px). A single file —
        //    thumbnails are generated on-the-fly via imgproxy at serving time.
        ProcessedImage processed;
        await using (var fileStream = file.OpenReadStream())
        {
            processed = await _imageProcessingService.ProcessAsync(fileStream, file.ContentType);
        }

        // 3. Generate the object key (the extension is NORMALIZED from the validated
        //    content-type, NOT from the user filename — anti-extension-spoofing).
        var objectKey = GenerateObjectKey(type, userId, processed.Extension);

        // 4. A single PUT into the object store. Everything after it that can
        //    fail is wrapped in the rollback below.
        await _objectStorage.PutAsync(objectKey, processed.Bytes, processed.ContentType);

        Activity.Current?.SetTag("upload.output_size_bytes", processed.Bytes.LongLength);

        // 5. DB record. If the write fails — roll back the S3 PUT.
        var newUpload = new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            TargetId = effectiveTarget,
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
            // Only for the types the bucket actually answers anonymously. The
            // policy grants GetObject per prefix, so handing back a public
            // address for anything else is a 200 carrying a link that returns
            // 403 — the object key is stored either way, and a serving path for
            // the closed types is what they are waiting on.
            Url = UploadFolder.AnonymouslyReadable.Contains(type)
                ? _objectStorage.BuildPublicUrl(objectKey)
                : null,
            CreatedUtc = now,
            ConfirmedUtc = now,
        };

        StoredUpload stored;
        try
        {
            stored = await _uploadRepository.AddAsync(newUpload);
        }
        catch
        {
            // Compensation for the PUT above, and the only chance there is: the
            // object is in the bucket and the only row that would ever have named
            // it does not exist, while the orphan sweeper walks rows. The catch
            // has to stay on this side of the repository call — that is where the
            // object was written and where the key is still known.
            await _objectStorage.DeleteAsync(objectKey);
            throw;
        }

        return MapToDto(stored);
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
    /// Refuses an upload whose target the caller has no right to.
    /// </summary>
    /// <remarks>
    /// Until this existed the endpoint checked only that a target was named, so any
    /// authenticated user could point a CharacterAvatar at any character. Two such
    /// rows turned every read of that character's room into a 500 for everyone, and
    /// nothing but a hand-edited row brought it back.
    ///
    /// The rule per type comes from the module that owns the entity — see
    /// IUploadTargetAuthorizer. Fails closed: a type with no authorizer is refused,
    /// so adding one to the enum without a rule breaks the upload rather than
    /// opening it. That path is unreachable today and is asserted, not assumed.
    /// </remarks>
    private async Task AuthorizeTargetAsync(UploadType type, Guid target)
    {
        var authorizer = _targetAuthorizers.FirstOrDefault(a => a.Type == type);
        if (authorizer == null)
        {
            throw new InvalidOperationException(
                $"No upload target authorizer for {type}");
        }

        await authorizer.EnsureAllowedAsync(target);
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
        var (uploads, totalCount) = await _uploadRepository.GetPageAsync(
            new UploadFilter
            {
                UserId = userId,
                Type = query.Type,
                Status = query.Status,
            },
            skip: query.Skip,
            take: query.Take);

        // Skip is an offset in entities, which is the slot PagingResult.Create
        // reads as a 1-based entity number — the same conversion PagingData does
        // for every other offset-paged list. The page number used to be handed
        // in here instead, and ceil(page / pageSize) reported page 1 for the
        // first twenty pages.
        var paging = new PagingInfo(PagingResult.Create(totalCount, query.Skip + 1, query.Take));
        return (uploads.Select(MapToDto), paging);
    }

    /// <summary>
    /// Object key: type folder + scope (userId) + a full random suffix. Not a
    /// content hash: the same image uploaded twice occupies two objects, and the
    /// key says nothing about what stands behind it. Keys are never rewritten (a
    /// replaced avatar allocates a fresh one), and that, not the shape of the key,
    /// is what the immutable cache headers on PUT rest on.
    ///
    /// The suffix is the whole identifier and not eight characters of it, because
    /// a key is never reused and never overwritten and the value has to be unique
    /// for as long as the bucket lives. What it is NOT is access control: the
    /// bucket answers anonymously on the declared public prefixes only, and an
    /// attachment in a closed room is not under one of them. It used to be — the
    /// grant covered the whole bucket, listing included — and that made this
    /// paragraph read like a security argument, which the length of a key cannot
    /// be. The extension is accepted as a validated, normalized string.
    /// </summary>
    private string GenerateObjectKey(UploadType type, Guid userId, string normalizedExtension)
    {
        // The prefix is not chosen here: the bucket policy grants anonymous reads
        // per prefix, so the two have to say the same thing about a type.
        var folder = UploadFolder.For(type);

        var uniqueId = Guid.NewGuid().ToString("N");
        var ext = string.IsNullOrEmpty(normalizedExtension) ? string.Empty : normalizedExtension;
        var keyName = $"{userId:N}_{uniqueId}{ext}";

        return string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{keyName}"
            : $"{_cdnConfig.Folder}/{folder}/{keyName}";
    }

    private static Shared.Dto.Upload MapToDto(StoredUpload upload)
    {
        return new Shared.Dto.Upload
        {
            Id = upload.Id,
            UserId = upload.UserId,
            // Only populated when the store resolved the owner (the moderation
            // list and single-upload queries); null right after DirectUpload.
            UploaderUsername = upload.OwnerUsername,
            Type = upload.Type,
            TargetId = upload.TargetId,
            OriginalFileName = upload.FileName,
            ContentType = upload.ContentType,
            SizeBytes = upload.SizeBytes,
            Status = upload.Status,
            Url = upload.Url,
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc,
        };
    }
}
