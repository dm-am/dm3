using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// File upload information.
/// </summary>
public class Upload
{
    /// <summary>Upload unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner user identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Upload type / purpose.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType Type { get; set; }

    /// <summary>Target entity identifier (e.g. user ID for avatar, post ID for attachment).</summary>
    public Guid? TargetId { get; set; }

    /// <summary>Original (sanitized) file name.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>MIME content type (always taken from validated magic-bytes).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Processing status.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus Status { get; set; }

    /// <summary>
    /// Source file URL (≤1024 px, EXIF stripped). Thumbnail-варианты
    /// генерируются on-the-fly через imgproxy при serving — клиент берет
    /// этот URL для original-варианта и собирает imgproxy URL'ы для
    /// thumbnails через picture-объект в User/UserProfile/Character DTO.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Confirmation timestamp (UTC).</summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }
}

/// <summary>
/// Query parameters for listing uploads.
/// </summary>
public class UploadsQuery
{
    /// <summary>Filter by upload type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType? Type { get; set; }

    /// <summary>Filter by processing status.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus? Status { get; set; }

    /// <summary>Filter by user ID (admin only).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Page number (1-based).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Номер страницы должен быть не менее 1")]
    public int Number { get; set; } = 1;

    /// <summary>Number of items per page (1-100).</summary>
    [Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 до 100")]
    public int Size { get; set; } = 20;
}
