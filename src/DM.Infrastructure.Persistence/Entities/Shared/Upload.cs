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
    /// Linked entity identifier
    /// </summary>
    public Guid? EntityId { get; set; }

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
    /// S3 object key (for presigned URL workflow)
    /// </summary>
    [MaxLength(500)]
    public string ObjectKey { get; set; } = null!;

    /// <summary>
    /// Path to download or view the file (public URL)
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// Medium size file path (for images)
    /// </summary>
    [MaxLength(500)]
    public string? MediumFilePath { get; set; }

    /// <summary>
    /// Small/thumbnail file path (for images)
    /// </summary>
    [MaxLength(500)]
    public string? SmallFilePath { get; set; }

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

    // NOTE: EntityId is a polymorphic FK that can reference User, Game, Character, or Post
    // depending on UploadType. No navigation properties are defined to avoid EF Core
    // creating FK constraints on a polymorphic column. Referential integrity is managed
    // by application logic.
}
