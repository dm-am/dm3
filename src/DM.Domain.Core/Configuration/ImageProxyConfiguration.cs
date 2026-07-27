namespace DM.Domain.Core.Configuration;

/// <summary>
/// imgproxy configuration — on-the-fly image transforms (resize + format
/// negotiation AVIF/WebP/JPEG).
///
/// The backend stores a single source file in S3 and generates signed imgproxy URLs
/// for thumbnail variants when projecting to DTOs. No pre-generated
/// thumbnails in storage.
/// </summary>
public class ImageProxyConfiguration
{
    /// <summary>
    /// Public imgproxy URL (where the frontend loads transformed images from).
    /// Dev: http://localhost:8080. Prod: https://img.dm.com or similar.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 key (hex). Used to sign URLs so that
    /// nobody can request an arbitrary transform.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 salt (hex). Concatenated with the path before HMAC.
    /// </summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Source URL prefix for imgproxy (where it fetches the original).
    /// For MinIO S3: <c>s3://dm-uploads/</c>. For an HTTPS CDN: <c>https://cdn.example.com/</c>.
    /// </summary>
    public string SourceUrlPrefix { get; set; } = string.Empty;
}
