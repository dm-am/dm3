using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Common;

/// <summary>
/// File upload information
/// </summary>
public class Upload
{
    /// <summary>
    /// Upload unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Owner user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Upload type/purpose
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType Type { get; set; }

    /// <summary>
    /// Target entity identifier (e.g., user ID for avatar, post ID for attachment)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Original file name
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Upload processing status
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus Status { get; set; }

    /// <summary>
    /// Original file URL (full size)
    /// </summary>
    public string? OriginalUrl { get; set; }

    /// <summary>
    /// Medium size URL (for images)
    /// </summary>
    public string? MediumUrl { get; set; }

    /// <summary>
    /// Small/thumbnail URL (for images)
    /// </summary>
    public string? SmallUrl { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Confirmation timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }
}

/// <summary>
/// Request to create presigned upload URL
/// </summary>
public class PresignRequest
{
    /// <summary>
    /// Upload type/purpose
    /// </summary>
    [Required(ErrorMessage = "Upload type is required")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType Type { get; set; }

    /// <summary>
    /// Target entity identifier (optional, depends on upload type)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Original file name (1-255 characters)
    /// </summary>
    /// <example>profile-photo.jpg</example>
    [Required(ErrorMessage = "File name is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "File name must be between 1 and 255 characters")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (e.g., image/jpeg, image/png)
    /// </summary>
    /// <example>image/jpeg</example>
    [Required(ErrorMessage = "Content type is required")]
    [StringLength(100, ErrorMessage = "Content type must not exceed 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-\+\.]+/[a-zA-Z0-9\-\+\.]+$", ErrorMessage = "Invalid MIME type format")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes (1 byte to 100 MB)
    /// </summary>
    [Range(1, 104857600, ErrorMessage = "File size must be between 1 byte and 100 MB")]
    public long SizeBytes { get; set; }
}

/// <summary>
/// Response with presigned upload URL
/// </summary>
public class PresignResponse
{
    /// <summary>
    /// Upload identifier (use to confirm upload after uploading file)
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Presigned URL for direct file upload (PUT request)
    /// </summary>
    /// <remarks>
    /// Upload the file using HTTP PUT to this URL.
    /// Include Content-Type header matching the requested content type.
    /// URL expires after the time specified in ExpiresAt.
    /// </remarks>
    public string PresignedUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL expiration time (UTC)
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Query parameters for listing uploads
/// </summary>
public class UploadsQuery
{
    /// <summary>
    /// Filter by upload type
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType? Type { get; set; }

    /// <summary>
    /// Filter by processing status
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus? Status { get; set; }

    /// <summary>
    /// Filter by user ID (admin only)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1")]
    public int Number { get; set; } = 1;

    /// <summary>
    /// Number of items per page (1-100)
    /// </summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
    public int Size { get; set; } = 20;
}
