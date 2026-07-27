namespace DM.Domain.Core.Uploads;

/// <summary>
/// Generator of signed imgproxy URLs for avatar thumbnail variants.
///
/// The backend stores a single source file; any size/format is generated on the fly
/// via imgproxy. The HMAC-SHA256 signature prevents abuse (otherwise anyone
/// could request an arbitrary transform and burn CPU).
/// </summary>
public interface IImgproxyUrlBuilder
{
    /// <summary>
    /// Generate a URL for a thumbnail of the given size (square center-crop).
    /// Format negotiation (AVIF/WebP/JPEG) is done by imgproxy based on the browser
    /// Accept header — a single URL for all formats.
    /// </summary>
    /// <param name="sourceObjectKey">S3 object key of the source file (without prefix).</param>
    /// <param name="size">Square side in pixels (100, 400, etc).</param>
    /// <returns>Signed URL for GET.</returns>
    string BuildSquareThumbnail(string sourceObjectKey, int size);
}
