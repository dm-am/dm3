using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Unified image processing service for the Common Upload system.
/// Handles validation, thumbnail generation, and resizing for image uploads.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Validate content type for the given upload type. Throws HttpBadRequestException on failure.
    /// </summary>
    void ValidateImageContentType(string contentType);

    /// <summary>
    /// Check if upload type requires image processing (thumbnails)
    /// </summary>
    bool IsImageType(UploadType type);

    /// <summary>
    /// Download image from S3, generate center-cropped thumbnails, upload back to S3.
    /// Returns (mediumPublicUrl, smallPublicUrl).
    /// </summary>
    /// <param name="objectKey">S3 object key of the original image</param>
    /// <param name="generatePublicUrl">Function to generate public URL from object key</param>
    Task<(string mediumUrl, string smallUrl)> ProcessAndUploadThumbnails(
        string objectKey, Func<string, string> generatePublicUrl);
}
