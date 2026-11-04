using System;
using System.Collections.Generic;
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
    public UploadStatus Status { get; set; }

    /// <summary>
    /// Source file URL (≤1024 px, EXIF stripped). The thumbnails of an avatar
    /// come in the picture object of the User/UserProfile/Character DTOs.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Address of the endpoint that serves the file's bytes to whoever is allowed
    /// to see them.
    /// </summary>
    /// <remarks>
    /// Present also without a public <see cref="Url"/>. It grants nothing: the
    /// endpoint behind it re-decides on every request.
    /// </remarks>
    public string ContentUrl { get; set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Confirmation timestamp (UTC).</summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }

    /// <summary>
    /// Whether the caller may open the file. False for a file the post keeps from
    /// him, its mark or a hidden block: a list names it without a link and without
    /// a preview, and its address answers him as a file that is not there.
    /// </summary>
    public bool CanOpen { get; set; }

    /// <summary>
    /// Who sees a file of a post marked "Выбранные", the words after "Получатели:",
    /// leads first: "DungeonMaster, Assistant, Вася". Null for a file every reader
    /// of the post sees, and for a caller who may not open it.
    /// </summary>
    public string? Recipients { get; set; }

    /// <summary>
    /// The characters chosen to see the file, for the checkboxes of its author: one
    /// gone from the room keeps his box by the name the mark froze. Null when
    /// <see cref="Recipients"/> is.
    /// </summary>
    public IReadOnlyList<UploadAddressee>? Addressees { get; set; }
}

/// <summary>
/// A character a file of a post is marked for.
/// </summary>
public class UploadAddressee
{
    /// <summary>The character.</summary>
    public Guid Id { get; set; }

    /// <summary>His name when he was chosen: the room may no longer know it.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Who sees a file of a post: the choice on its plate.
/// </summary>
public class UpdateUploadAudienceRequest
{
    /// <summary>
    /// "All" for every reader of the post, "Selected" for the author, the leads and the characters named.
    /// Required: a body without it would otherwise read as "All" and open the file to everybody.
    /// </summary>
    public UploadVisibleTo? VisibleTo { get; set; }

    /// <summary>The characters chosen, from those let into the room; read only for "Selected".</summary>
    public IReadOnlyList<Guid>? CharacterIds { get; set; }
}

/// <summary>
/// Query parameters for listing uploads.
/// </summary>
public class UploadsQuery : PagingQuery
{
    /// <summary>Filter by upload type.</summary>
    public UploadType? Type { get; set; }

    /// <summary>Filter by processing status.</summary>
    public UploadStatus? Status { get; set; }
}
