using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.Uploading.Authorization;
using DM.Services.Uploading.BusinessProcesses.ImageProcessing;
using DM.Services.Uploading.Configuration;
using DM.Web.API.Dto.Common;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DbUpload = DM.Services.DataAccess.BusinessObjects.Common.Upload;

namespace DM.Web.API.Services.Common;

/// <inheritdoc />
internal class UploadApiService : IUploadApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserReadingService _userReadingService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAmazonS3 _s3Client;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly CdnConfiguration _cdnConfig;

    private const int PresignedUrlExpirationMinutes = 15;
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <inheritdoc />
    public UploadApiService(
        DmDbContext dbContext,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserReadingService userReadingService,
        IDateTimeProvider dateTimeProvider,
        IAmazonS3 s3Client,
        IImageProcessingService imageProcessingService,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _dbContext = dbContext;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userReadingService = userReadingService;
        _dateTimeProvider = dateTimeProvider;
        _s3Client = s3Client;
        _imageProcessingService = imageProcessingService;
        _cdnConfig = cdnOptions.Value;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Upload>> GetUploads(UploadsQuery query, string? userLogin, bool all)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // Case 1: scope=all → admin wants all uploads
        if (all)
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListAll);
            return await GetUploadsInternal(query, userId: null);
        }

        // Case 2: userLogin specified → admin wants specific user's uploads
        if (!string.IsNullOrWhiteSpace(userLogin))
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListUser);
            var user = await _userReadingService.Get(userLogin);
            return await GetUploadsInternal(query, user.UserId);
        }

        // Case 3: Default → current user's uploads (same for user and admin)
        return await GetUploadsInternal(query, currentUserId);
    }

    /// <inheritdoc />
    public async Task<Envelope<Upload>> GetUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _dbContext.Uploads
            .Where(u => u.UploadId == id)
            .FirstOrDefaultAsync();

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Upload not found");
        }

        // Users can only view their own uploads (admins handled separately)
        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Admin)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        return new Envelope<Upload>(MapToDto(upload));
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

        // Users can only delete their own uploads (admins handled separately)
        if (upload.UserId != userId && _identityProvider.Current.User.Role < UserRole.Admin)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        upload.IsRemoved = true;
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Envelope<PresignResponse>> RequestPresignedUrl(PresignRequest request)
    {
        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        // Validate content type for image uploads
        if (_imageProcessingService.IsImageType(request.Type))
        {
            _imageProcessingService.ValidateImageContentType(request.ContentType);
            if (request.SizeBytes > MaxImageSizeBytes)
            {
                throw new HttpBadRequestException(
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["sizeBytes"] = "Максимальный размер изображения: 10 МБ"
                    });
            }
        }

        // Generate unique object key
        var objectKey = GenerateObjectKey(request.Type, userId, request.FileName);

        // Create pending upload record
        var upload = new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            Type = request.Type,
            Status = UploadStatus.Pending,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            ObjectKey = objectKey,
            EntityId = request.TargetId,
            Original = true,
            CreatedUtc = now
        };

        await _dbContext.Uploads.AddAsync(upload);
        await _dbContext.SaveChangesAsync();

        // Generate presigned URL for PUT
        var presignedUrl = GeneratePresignedPutUrl(objectKey, request.ContentType);

        return new Envelope<PresignResponse>(new PresignResponse
        {
            UploadId = upload.UploadId,
            PresignedUrl = presignedUrl,
            ExpiresAt = now.AddMinutes(PresignedUrlExpirationMinutes)
        });
    }

    /// <inheritdoc />
    public async Task<Envelope<Upload>> ConfirmUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _dbContext.Uploads
            .Where(u => u.UploadId == id)
            .FirstOrDefaultAsync();

        if (upload == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, "Upload not found");
        }

        if (upload.UserId != userId)
        {
            throw new HttpException(System.Net.HttpStatusCode.Forbidden, "Access denied");
        }

        if (upload.Status != UploadStatus.Pending)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest,
                $"Upload is not in pending status. Current status: {upload.Status}");
        }

        // Check if presigned URL has expired
        var expirationTime = upload.CreatedUtc.AddMinutes(PresignedUrlExpirationMinutes);
        if (_dateTimeProvider.Now > expirationTime)
        {
            upload.Status = UploadStatus.Failed;
            await _dbContext.SaveChangesAsync();
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Upload session expired");
        }

        // Verify file exists in S3
        var fileExists = await VerifyFileExists(upload.ObjectKey);
        if (!fileExists)
        {
            throw new HttpException(System.Net.HttpStatusCode.BadRequest,
                "File not found in storage. Please upload the file first.");
        }

        // Generate public URL
        var publicUrl = GeneratePublicUrl(upload.ObjectKey);

        // Generate thumbnails for image uploads
        string? mediumUrl = null;
        string? smallUrl = null;
        if (_imageProcessingService.IsImageType(upload.Type))
        {
            (mediumUrl, smallUrl) = await _imageProcessingService.ProcessAndUploadThumbnails(
                upload.ObjectKey, GeneratePublicUrl);
        }

        upload.FilePath = publicUrl;
        upload.MediumFilePath = mediumUrl;
        upload.SmallFilePath = smallUrl;
        upload.Status = UploadStatus.Confirmed;
        upload.ConfirmedUtc = _dateTimeProvider.Now;

        await _dbContext.SaveChangesAsync();

        return new Envelope<Upload>(MapToDto(upload));
    }

    /// <inheritdoc />
    public async Task<Envelope<Upload>> DirectUpload(IFormFile file, UploadType type, Guid? targetId)
    {
        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        // Validate content type and size for image uploads
        if (_imageProcessingService.IsImageType(type))
        {
            _imageProcessingService.ValidateImageContentType(file.ContentType);
            if (file.Length > MaxImageSizeBytes)
            {
                throw new HttpBadRequestException(
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["file"] = "Максимальный размер изображения: 10 МБ"
                    });
            }
        }

        // Generate object key and upload original to S3
        var objectKey = GenerateObjectKey(type, userId, file.FileName);
        await using var stream = file.OpenReadStream();
        await UploadStreamToS3(objectKey, stream, file.ContentType);

        // Generate public URL for original
        var publicUrl = GeneratePublicUrl(objectKey);

        // Process image: generate thumbnails if applicable
        string? mediumUrl = null;
        string? smallUrl = null;
        if (_imageProcessingService.IsImageType(type))
        {
            (mediumUrl, smallUrl) = await _imageProcessingService.ProcessAndUploadThumbnails(
                objectKey, GeneratePublicUrl);
        }

        // Create confirmed upload record
        var upload = new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            ObjectKey = objectKey,
            EntityId = targetId,
            Original = true,
            FilePath = publicUrl,
            MediumFilePath = mediumUrl,
            SmallFilePath = smallUrl,
            CreatedUtc = now,
            ConfirmedUtc = now
        };

        await _dbContext.Uploads.AddAsync(upload);
        await _dbContext.SaveChangesAsync();

        return new Envelope<Upload>(MapToDto(upload));
    }

    private async Task UploadStreamToS3(string objectKey, System.IO.Stream stream, string contentType)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType
        };
        await _s3Client.PutObjectAsync(putRequest);
    }

    private async Task<ListEnvelope<Upload>> GetUploadsInternal(UploadsQuery query, Guid? userId)
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

        var paging = new Paging(PagingResult.Create(totalCount, query.Number, query.Size));
        return new ListEnvelope<Upload>(uploads.Select(MapToDto), paging);
    }

    private string GenerateObjectKey(UploadType type, Guid userId, string fileName)
    {
        var folder = type switch
        {
            UploadType.UserAvatar => "avatars",
            UploadType.CharacterAvatar => "characters",
            UploadType.PostAttachment => "posts",
            _ => "misc"
        };

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var extension = System.IO.Path.GetExtension(fileName);
        var safeFileName = $"{userId:N}_{uniqueId}{extension}";

        return string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{safeFileName}"
            : $"{_cdnConfig.Folder}/{folder}/{safeFileName}";
    }

    private string GeneratePresignedPutUrl(string objectKey, string contentType)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(PresignedUrlExpirationMinutes),
            ContentType = contentType
        };

        return _s3Client.GetPreSignedURL(request);
    }

    private async Task<bool> VerifyFileExists(string objectKey)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_cdnConfig.BucketName, objectKey);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private string GeneratePublicUrl(string objectKey)
    {
        return new UriBuilder(new Uri(_cdnConfig.PublicUrl))
        {
            Path = $"{_cdnConfig.BucketName}/{objectKey}"
        }.ToString();
    }

    private static Upload MapToDto(DbUpload upload)
    {
        return new Upload
        {
            Id = upload.UploadId,
            UserId = upload.UserId,
            Type = upload.Type,
            TargetId = upload.EntityId,
            OriginalFileName = upload.FileName ?? string.Empty,
            ContentType = upload.ContentType ?? string.Empty,
            SizeBytes = upload.SizeBytes,
            Status = upload.Status,
            OriginalUrl = upload.FilePath,
            MediumUrl = upload.MediumFilePath,
            SmallUrl = upload.SmallFilePath,
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc
        };
    }
}
