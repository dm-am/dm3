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
    /// Target user (для Type=UserAvatar). Mutually exclusive с TargetCharacterId и TargetPostId.
    /// Type-discriminated по <see cref="Type"/>; CHECK-constraint в БД обеспечивает консистентность.
    /// </summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Target character (для Type=CharacterAvatar). Mutually exclusive с TargetUserId и TargetPostId.
    /// </summary>
    public Guid? TargetCharacterId { get; set; }

    /// <summary>
    /// Target post (для Type=PostAttachment). Mutually exclusive с TargetUserId и TargetCharacterId.
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
    /// S3 object key. Hash-based, immutable: для thumbnails ключи выводятся
    /// заменой расширения и добавлением суффикса (_m/_s). Используется
    /// GC и cleanup-логикой; FilePath/MediumFilePath/SmallFilePath — public URL
    /// для рендера.
    /// </summary>
    [MaxLength(500)]
    public string ObjectKey { get; set; } = null!;

    /// <summary>
    /// Public URL к source-файлу (≤1024 px, EXIF-stripped). Один файл per
    /// upload — thumbnail-варианты генерируются on-the-fly через imgproxy
    /// при serving, не пре-генерируются.
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

    // Navigation-свойства для target-колонок не определены умышленно:
    // FK-constraints настраиваются через OnModelCreating, а navigation
    // на стороне Upload не нужны (consumers всегда ходят с owner-side
    // через User.AvatarUploadId / Character.AvatarUploadId).
}
