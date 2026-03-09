namespace DM.Domain.Core.Configuration;

/// <summary>
/// CDN configuration
/// </summary>
public class CdnConfiguration
{
    /// <summary>
    /// Service URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Public CDN URL
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// AWS region
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Files folder
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Private access key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Public access key
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Relative file destination
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// CDN is MinIO
    /// </summary>
    public S3Provider Provider { get; set; }
}
