using System;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Service for uploading images for display on website
/// </summary>
public interface IPublicImageService : IObsoleteUploadsCleanup
{
    /// <summary>
    /// Upload original image and also its cropped it and resized to medium and small size versions
    /// </summary>
    /// <param name="createUpload">Upload data containing the image</param>
    /// <returns>Tuple of original, medium, and small size uploads</returns>
    Task<(Upload original, Upload medium, Upload small)> UploadAsync(CreateUpload createUpload);
}
