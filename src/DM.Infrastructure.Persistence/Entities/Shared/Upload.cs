using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for user uploaded content
/// </summary>
[Table("Uploads")]
public class Upload : ISoftDeletable
{
    /// <summary>
    /// Upload identifier
    /// </summary>
    [Key]
    public Guid UploadId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Confirmation moment (UTC) - when file was uploaded and confirmed
    /// </summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }

    /// <summary>
    /// Target user (for Type=UserAvatar). Mutually exclusive with TargetCharacterId and TargetPostId.
    /// Type-discriminated by <see cref="Type"/>; a DB CHECK constraint enforces consistency.
    /// </summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Target character (for Type=CharacterAvatar). Mutually exclusive with TargetUserId and TargetPostId.
    /// </summary>
    public Guid? TargetCharacterId { get; set; }

    /// <summary>
    /// Target post (for Type=PostAttachment). Mutually exclusive with TargetUserId and TargetCharacterId.
    /// </summary>
    public Guid? TargetPostId { get; set; }

    /// <summary>
    /// Owner identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Upload type/purpose
    /// </summary>
    public UploadType Type { get; set; }

    /// <summary>
    /// Upload processing status
    /// </summary>
    public UploadStatus Status { get; set; }

    /// <summary>
    /// Flag of file source: original or modified
    /// </summary>
    public bool Original { get; set; }

    /// <summary>
    /// MIME content type
    /// </summary>
    [MaxLength(100)]
    public string ContentType { get; set; } = null!;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Intrinsic width of the stored file in pixels, measured after the upload
    /// pipeline's downscale. Null for a row written before the pipeline recorded
    /// it — a reader that needs the aspect ratio has to cope with not knowing it.
    /// </summary>
    /// <remarks>
    /// Kept so a page can reserve the exact box the picture will occupy: the
    /// source file preserves the aspect ratio, so without the pair every layout
    /// showing it either guesses a square or reflows once the browser decodes.
    /// </remarks>
    public int? Width { get; set; }

    /// <summary>
    /// Intrinsic height of the stored file in pixels, on the same terms as
    /// <see cref="Width"/>. The two are written together or not at all.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// S3 object key. Random per upload, not a content hash: the same file
    /// uploaded twice gets two keys, and nothing deduplicates by content. What
    /// the key does guarantee is immutability — it is never reused, so the object
    /// behind it never changes and may be cached forever. Used by GC and cleanup
    /// logic; FilePath is the public URL for rendering. There are no derived
    /// keys: thumbnail variants are made on-the-fly by imgproxy at serving time.
    /// </summary>
    [MaxLength(500)]
    public string ObjectKey { get; set; } = null!;

    /// <summary>
    /// Public URL to the source file (≤1024 px, EXIF-stripped). One file per
    /// upload — thumbnail variants are generated on-the-fly via imgproxy
    /// at serving time, not pre-generated.
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// Display name for the file
    /// </summary>
    [MaxLength(255)]
    public string? FileName { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Owner
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Owner { get; set; } = null!;

    /// <summary>
    /// User who deleted the upload
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    // Navigation properties for the target columns are intentionally not defined:
    // FK constraints are configured via OnModelCreating, and navigations
    // on the Upload side are not needed. The user avatar is reached from the owner
    // side via User.AvatarUploadId; a character has no such column, so its portrait
    // is read by querying Uploads on TargetCharacterId.
}
