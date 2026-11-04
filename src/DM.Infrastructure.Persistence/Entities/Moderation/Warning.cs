using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model for user warning
/// </summary>
/// <remarks>
/// Not soft-deletable: a warning is the record of a moderator's decision. Only the
/// points can be taken back, by a change with an author of its own, see
/// <see cref="PointsChangedByUserId" />.
/// </remarks>
[Table("Warnings")]
public class Warning : IAdministrated
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    [Key]
    public Guid WarningId { get; set; }

    /// <inheritdoc />
    public Guid TargetUserId { get; set; }

    /// <inheritdoc />
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Warning causation entity identifier (e.g. topic, comment, etc.)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Type of the entity that caused the warning
    /// </summary>
    public WarningEntityType EntityType { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Moderation message
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Copy of the offending text, taken when the warning was issued. Null when
    /// the warning names no entity, or when the entity kind carries no text the
    /// moderation area reads.
    /// </summary>
    /// <remarks>
    /// Written once at issue time and never rewritten, because the author may edit
    /// or delete the entity afterwards.
    /// </remarks>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// The status of the warned profile when a warning on the profile was
    /// issued. Null for every other kind, and when the profile had no status.
    /// </summary>
    /// <remarks>
    /// "О себе" of the same profile goes to <see cref="EntitySnapshot" />, which
    /// is the copy of the text the warning was given for; the status is a line of
    /// its own, as long as the profile allows it.
    /// </remarks>
    [MaxLength(200)]
    public string? ProfileStatusSnapshot { get; set; }

    /// <summary>
    /// The upload the avatar of the warned profile was when a warning on the
    /// profile was issued. Null for every other kind, when the profile had no
    /// avatar, and once the upload is gone from the table.
    /// </summary>
    /// <remarks>
    /// A reference that pins the upload against the orphan sweeper until moderation
    /// deletes it; the key then clears this column (ON DELETE SET NULL).
    /// </remarks>
    public Guid? ProfileAvatarUploadId { get; set; }

    /// <summary>
    /// Warning points as they stand now: what the violation scale of the user
    /// counts
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning points as the moderator who issued the warning set them. Equal to
    /// <see cref="Points" /> until somebody changes the points.
    /// </summary>
    /// <remarks>
    /// Kept beside the current value because a change writes over Points.
    /// </remarks>
    public int OriginalPoints { get; set; }

    /// <summary>
    /// Moderator who last changed the points (null while nobody has)
    /// </summary>
    /// <remarks>
    /// No cascade: deleting a moderator's account must not delete the warnings he corrected.
    /// </remarks>
    public Guid? PointsChangedByUserId { get; set; }

    /// <summary>
    /// When the points were last changed
    /// </summary>
    public DateTimeOffset? PointsChangedUtc { get; set; }

    /// <summary>
    /// Why the points were changed, written by whoever changed them
    /// </summary>
    /// <remarks>
    /// Required of the change and shown wherever the warning is shown, so the first
    /// verdict is never read alone.
    /// </remarks>
    public string? PointsChangeText { get; set; }

    /// <inheritdoc />
    [ForeignKey(nameof(TargetUserId))]
    public virtual User TargetUser { get; set; } = null!;

    /// <inheritdoc />
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Moderator who last changed the points
    /// </summary>
    [ForeignKey(nameof(PointsChangedByUserId))]
    public virtual User? PointsChangedBy { get; set; }

    /// <summary>
    /// The avatar the warned profile carried when the warning was issued
    /// </summary>
    /// <remarks>
    /// Behind the soft-delete filter of uploads like every navigation to one,
    /// and the pinned avatar is usually a deleted row: the read of the picture
    /// lifts the filter and states which deletion it honours by hand.
    /// </remarks>
    [ForeignKey(nameof(ProfileAvatarUploadId))]
    public virtual Upload? ProfileAvatarUpload { get; set; }
}
