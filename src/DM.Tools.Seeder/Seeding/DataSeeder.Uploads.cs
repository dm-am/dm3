using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using Microsoft.EntityFrameworkCore;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    private const string DiopsideImage = "DM.Tools.Seeder.Assets.Seed.diopside.jpg";
    private const string ChuckImage = "DM.Tools.Seeder.Assets.Seed.Chuck.png";
    private const string SolohinLexImage = "DM.Tools.Seeder.Assets.Seed.SolohinLex.jpg";
    private const string SolohinLexInfo = "DM.Tools.Seeder.Assets.Seed.SolohinLex.bbcode";

    // Runs a seed image through the real image pipeline: magic bytes, EXIF strip, resize,
    // thumbnails, S3 PUTs with Cache-Control: immutable.
    private async Task<DbUpload> SeedImageFromBytesAsync(
        byte[] imageBytes,
        string declaredContentType,
        string sourceFileName,
        UploadType type,
        Guid uploadId,
        Guid userId,
        Guid? entityId,
        DateTimeOffset uploadedUtc)
    {
        ProcessedImage processed;
        await using (var input = new MemoryStream(imageBytes, writable: false))
        {
            processed = await _imageProcessing.ProcessAsync(input, declaredContentType, type);
        }

        // UploadFolder decides the folder and throws on an unknown type.
        var folder = UploadFolder.For(type);
        var objectKey = $"{folder}/{userId:N}_{uploadId:N}{processed.Extension}";

        await PutSeedObjectAsync(objectKey, processed.Bytes, processed.ContentType);
        foreach (var thumbnail in processed.Thumbnails)
        {
            await PutSeedObjectAsync(
                AvatarThumbnail.AddressOf(objectKey, thumbnail.Size), thumbnail.Bytes, AvatarThumbnail.ContentType);
        }

        var upload = new DbUpload
        {
            UploadId = uploadId,
            CreatedUtc = uploadedUtc,
            ConfirmedUtc = uploadedUtc,
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            Original = true,
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            Width = processed.Width,
            Height = processed.Height,
            ObjectKey = objectKey,
            // Only for the prefixes the bucket answers anonymously, as the upload endpoint does.
            FilePath = UploadFolder.AnonymouslyReadable.Contains(type)
                ? BuildPublicUrl(objectKey)
                : null,
            FileName = sourceFileName,
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

    // Read UTF-8 text from an embedded assembly resource; a missing one fails the seed.
    private static async Task<string> LoadEmbeddedTextAsync(string resourceName)
    {
        await using var stream = OpenEmbedded(resourceName);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    // Read the bytes of an embedded seed image; a missing one fails the seed.
    private static byte[] ReadEmbeddedSeedBytes(string resourceName)
    {
        using var resourceStream = OpenEmbedded(resourceName);
        using var ms = new MemoryStream();
        resourceStream.CopyTo(ms);
        return ms.ToArray();
    }

    private static Stream OpenEmbedded(string resourceName) =>
        typeof(DataSeeder).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException(
            $"Embedded resource '{resourceName}' not found — check DM.Tools.Seeder.csproj <EmbeddedResource> entry.");
}
