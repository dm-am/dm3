namespace DM.Domain.Core.Dto;

/// <summary>
/// Avatar source file (one per upload). SSOT for projections of entities
/// that have an avatar (User, Character).
///
/// The domain level stores ONLY the source URL and object key. Thumbnail variants
/// (small/medium) are generated on-the-fly at the API layer via imgproxy when
/// mapping to DTOs (see UserMapper.ToUserPicture).
/// </summary>
public sealed class AvatarPicture
{
    /// <summary>
    /// S3 object key of the source file (e.g. <c>avatars/{userId:N}_{8hex}.jpg</c>).
    /// Used by the imgproxy URL builder at the API layer. Null if the entity
    /// has no avatar.
    /// </summary>
    public string? SourceObjectKey { get; set; }

    /// <summary>
    /// Direct public URL to the source file. Aspect-preserving original
    /// (≤1024 px). EXIF stripped. Null if there is no avatar.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Intrinsic width of the source file in pixels. Null when the upload row
    /// carries no measurement — the thumbnails are square by construction, but
    /// the original is not, and a consumer that lays out a box for it needs the
    /// ratio before the picture arrives.
    /// </summary>
    public int? SourceWidth { get; set; }

    /// <summary>
    /// Intrinsic height of the source file in pixels; travels with
    /// <see cref="SourceWidth"/> or not at all.
    /// </summary>
    public int? SourceHeight { get; set; }
}
