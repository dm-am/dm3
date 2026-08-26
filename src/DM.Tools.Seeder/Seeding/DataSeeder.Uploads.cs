using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Runs a seed image through the real image pipeline:
    /// magic-byte → EXIF strip → resize → a single S3 PUT of the source object
    /// with Cache-Control: immutable. Returns an Upload entity ready
    /// for Add() into the DbContext.
    /// </summary>
    private async Task<DM.Infrastructure.Persistence.Entities.Shared.Upload> SeedAvatarFromBytesAsync(
        byte[] imageBytes,
        string declaredContentType,
        string sourceFileName,
        UploadType type,
        Guid uploadId,
        Guid userId,
        Guid? entityId,
        DateTimeOffset now)
    {
        // The same pipeline as user uploads: magic-byte
        // validation, EXIF strip, downscale to 1024 px. A single source file —
        // imgproxy makes thumbnails on the fly at serving time.
        ProcessedImage processed;
        await using (var input = new MemoryStream(imageBytes, writable: false))
        {
            processed = await _imageProcessing.ProcessAsync(input, declaredContentType, type);
        }

        // The third copy of the same list, and the one that disagreed: a type
        // outside the two it named landed in "misc", a prefix nothing serves and
        // no policy grants. UploadFolder is where the mapping is decided, and it
        // throws on an unknown type rather than inventing a folder for it.
        var folder = UploadFolder.For(type);
        var objectKey = string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{userId:N}_{uploadId:N}{processed.Extension}"
            : $"{_cdnConfig.Folder}/{folder}/{userId:N}_{uploadId:N}{processed.Extension}";

        await PutSeedObjectAsync(objectKey, processed.Bytes, processed.ContentType);

        var upload = new DM.Infrastructure.Persistence.Entities.Shared.Upload
        {
            UploadId = uploadId,
            CreatedUtc = now,
            ConfirmedUtc = now,
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            Original = true,
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            Width = processed.Width,
            Height = processed.Height,
            ObjectKey = objectKey,
            // Only for the prefixes the bucket answers anonymously, the same rule
            // the upload endpoint applies. A public address stored for a closed
            // prefix is a link that returns 403 and a seed that disagrees with
            // every row the site writes itself.
            FilePath = UploadFolder.AnonymouslyReadable.Contains(type)
                ? BuildPublicUrl(objectKey)
                : null,
            FileName = sourceFileName,
            IsRemoved = false,
        };
        switch (type)
        {
            case UploadType.UserAvatar: upload.TargetUserId = entityId; break;
            case UploadType.CharacterAvatar: upload.TargetCharacterId = entityId; break;
            case UploadType.PostAttachment: upload.TargetPostId = entityId; break;
        }
        return upload;
    }

    private async Task PutSeedObjectAsync(string objectKey, byte[] bytes, string contentType)
    {
        await _s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            InputStream = new MemoryStream(bytes, writable: false),
            ContentType = contentType,
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        });
    }

    private string BuildPublicUrl(string objectKey) =>
        new UriBuilder(new Uri(_cdnConfig.PublicUrl))
        {
            Path = $"{_cdnConfig.BucketName}/{objectKey}",
        }.ToString();

    /// <summary>
    /// Read UTF-8 text from an embedded assembly resource. Throws if
    /// no resource with that name is registered — that is a setup bug (a missing
    /// <c>&lt;EmbeddedResource&gt;</c> in the csproj); it must fail the seed
    /// loudly rather than silently writing an empty string into Info.
    /// </summary>
    private static async Task<string> LoadEmbeddedTextAsync(string resourceName)
    {
        var assembly = typeof(DataSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found — check DM.Tools.Seeder.csproj <EmbeddedResource> entry.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Read the bytes of an embedded seed avatar image resource. Throws
    /// <see cref="InvalidOperationException"/> if the resource is not embedded
    /// (deployment misconfig).
    /// </summary>
    private static byte[] ReadEmbeddedSeedBytes(string resourceName)
    {
        var assembly = typeof(DataSeeder).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found");

        using var ms = new MemoryStream();
        resourceStream.CopyTo(ms);
        return ms.ToArray();
    }
}
