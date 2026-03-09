using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Shared.Dto;

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
    [Required(ErrorMessage = "Тип загрузки обязателен")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType Type { get; set; }

    /// <summary>
    /// Target entity identifier (optional, depends on upload type)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Original file name (1-255 символов)
    /// </summary>
    /// <example>profile-photo.jpg</example>
    [Required(ErrorMessage = "Имя файла обязательно")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Имя файла должно быть от 1 and 255 символов")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (e.g., image/jpeg, image/png)
    /// </summary>
    /// <example>image/jpeg</example>
    [Required(ErrorMessage = "Тип содержимого обязателен")]
    [StringLength(100, ErrorMessage = "Тип содержимого не должен превышать 100 символов")]
    [RegularExpression(@"^[a-zA-Z0-9\-\+\.]+/[a-zA-Z0-9\-\+\.]+$", ErrorMessage = "Неверный формат MIME типа")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes (1 byte to 100 MB)
    /// </summary>
    [Range(1, 104857600, ErrorMessage = "Размер файла должен быть от 1 байта и 100 МБ")]
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
    [Range(1, int.MaxValue, ErrorMessage = "Номер страницы должен быть не менее 1")]
    public int Number { get; set; } = 1;

    /// <summary>
    /// Number of items per page (1-100)
    /// </summary>
    [Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 and 100")]
    public int Size { get; set; } = 20;
}
