using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.DataContracts;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.CrossDomain;

/// <summary>
/// DAL model for user uploaded content
/// </summary>
[Table("Uploads")]
public class Upload : IRemovable
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

    /// <summary>
    /// Owner
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Owner { get; set; } = null!;

    /// <summary>
    /// User profile (for user profile picture)
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual User? UserProfile { get; set; }

    /// <summary>
    /// Game (for game preview picture)
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Game.Game? Game { get; set; }

    /// <summary>
    /// Character (for character portrait)
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Character? Character { get; set; }

    /// <summary>
    /// Post (for post attachment)
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Post? Post { get; set; }
}
