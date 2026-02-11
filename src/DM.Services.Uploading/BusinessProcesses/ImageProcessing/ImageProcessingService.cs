using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Uploading.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DM.Services.Uploading.BusinessProcesses.ImageProcessing;

/// <inheritdoc />
internal class ImageProcessingService : IImageProcessingService
{
    private static readonly Size MediumSize = new(400, 400);
    private static readonly Size SmallSize = new(100, 100);

    private static readonly string[] AllowedImageContentTypes =
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private readonly IAmazonS3 _s3Client;
    private readonly CdnConfiguration _cdnConfig;

    public ImageProcessingService(
        IAmazonS3 s3Client,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _s3Client = s3Client;
        _cdnConfig = cdnOptions.Value;
    }

    /// <inheritdoc />
    public void ValidateImageContentType(string contentType)
    {
        if (!AllowedImageContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new HttpBadRequestException(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["file"] = "Допустимые форматы изображений: JPEG, PNG, WebP, GIF"
                });
        }
    }

    /// <inheritdoc />
    public bool IsImageType(UploadType type) =>
        type is UploadType.UserAvatar or UploadType.CharacterAvatar;

    /// <inheritdoc />
    public async Task<(string mediumUrl, string smallUrl)> ProcessAndUploadThumbnails(
        string objectKey, Func<string, string> generatePublicUrl)
    {
        // Download original from S3
        var getResponse = await _s3Client.GetObjectAsync(_cdnConfig.BucketName, objectKey);
        using var image = await Image.LoadAsync(getResponse.ResponseStream);

        // Validate dimensions
        if (image.Width > 8192 || image.Height > 8192)
        {
            throw new HttpBadRequestException(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["file"] = "Изображение слишком большое (максимум 8192x8192)"
                });
        }

        if (image.Width < 50 || image.Height < 50)
        {
            throw new HttpBadRequestException(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["file"] = "Изображение слишком маленькое (минимум 50x50)"
                });
        }

        // Resize original if too large (save storage)
        if (image.Width > 1024 || image.Height > 1024)
        {
            image.Mutate(c => c.Resize(new ResizeOptions
            {
                Size = new Size(1024, 1024),
                Mode = ResizeMode.Max
            }));

            await using var resizedStream = new MemoryStream();
            await image.SaveAsJpegAsync(resizedStream, new JpegEncoder { Quality = 90 });
            resizedStream.Position = 0;
            await UploadToS3(objectKey, resizedStream, "image/jpeg");
        }

        // Center-crop to square
        var cropRect = image.Height > image.Width
            ? new Rectangle(0, (image.Height - image.Width) / 2, image.Width, image.Width)
            : new Rectangle((image.Width - image.Height) / 2, 0, image.Height, image.Height);

        var jpegEncoder = new JpegEncoder { Quality = 85 };

        // Generate medium thumbnail (400x400)
        var mediumKey = GetThumbnailKey(objectKey, "_m");
        await using (var mediumStream = new MemoryStream())
        {
            await image.Clone(c => c.Crop(cropRect).Resize(MediumSize))
                .SaveAsJpegAsync(mediumStream, jpegEncoder);
            mediumStream.Position = 0;
            await UploadToS3(mediumKey, mediumStream, "image/jpeg");
        }

        // Generate small thumbnail (100x100)
        var smallKey = GetThumbnailKey(objectKey, "_s");
        await using (var smallStream = new MemoryStream())
        {
            await image.Clone(c => c.Crop(cropRect).Resize(SmallSize))
                .SaveAsJpegAsync(smallStream, jpegEncoder);
            smallStream.Position = 0;
            await UploadToS3(smallKey, smallStream, "image/jpeg");
        }

        return (generatePublicUrl(mediumKey), generatePublicUrl(smallKey));
    }

    private async Task UploadToS3(string objectKey, Stream stream, string contentType)
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

    private static string GetThumbnailKey(string objectKey, string suffix)
    {
        var ext = Path.GetExtension(objectKey);
        var baseName = objectKey[..^ext.Length];
        return $"{baseName}{suffix}.jpg";
    }
}
