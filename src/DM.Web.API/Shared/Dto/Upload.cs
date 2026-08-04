using System;
using System.Text.Json.Serialization;
using DM.Domain.Core.Dto;
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

    /// <summary>
    /// Owner username (profile ref). Populated on the moderation list views
    /// (all-uploads / by-user) so the "Загрузил" column can link to the
    /// uploader's profile without a separate lookup. Null when the owner
    /// navigation was not loaded (e.g. the DirectUpload response).
    /// </summary>
    public string? UploaderUsername { get; set; }

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
    /// Source file URL (≤1024 px, EXIF stripped). Thumbnail variants
    /// are generated on-the-fly via imgproxy at serving time — the client uses
    /// this URL for the original variant and builds imgproxy URLs for
    /// thumbnails via the picture object in the User/UserProfile/Character DTOs.
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
/// <remarks>
/// Pages with skip/take, inherited from <see cref="PagingQuery"/>, like the
/// other twenty-seven offset-paged lists. It used to page with number/size and
/// was the only endpoint that did, which cost the shared paging component on
/// the client a branch of its own — and the page number was handed to
/// PagingResult.Create in the slot that takes an entity index, so every page
/// after the first came back declaring itself page 1.
/// </remarks>
public class UploadsQuery : PagingQuery
{
    /// <summary>Filter by upload type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadType? Type { get; set; }

    /// <summary>Filter by processing status.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus? Status { get; set; }
}
