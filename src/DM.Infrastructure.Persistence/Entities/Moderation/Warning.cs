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
/// Not soft-deletable, for the reason the ban is not: a warning is a record of
/// what a moderator decided, and the decision is never taken back. What can be
/// taken back are the points, and that is a change with an author of its own -
/// see <see cref="PointsChangedByUserId" />. It used to carry IsRemoved and the
/// global soft-delete filter with it, so undoing a warning erased it from the
/// open log, from the moderation list and from the moderator's own history at
/// once, and nobody could read who had undone what.
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
    /// The warning used to point at the entity and nothing else, and the author
    /// may edit that entity afterwards: the sentence the warning was given for
    /// disappeared, and no edit history on the site keeps the previous text. This
    /// column is the evidence — written once at issue time and never rewritten,
    /// so it also outlives the deletion of the entity it was taken from.
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
    /// A reference and not a copy: the warning pins the upload, and the orphan
    /// sweeper leaves a pinned upload alone for as long as nobody but its owner
    /// marked it deleted. Replacing an avatar marks the previous one deleted with
    /// no author, so the picture outlives the replacement; moderation deleting it
    /// lets the sweeper take it as any other, and the key then clears this
    /// column (ON DELETE SET NULL).
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
    /// Kept beside the current value rather than derived from it: the change
    /// writes over Points, and without this column what the warning originally
    /// weighed is gone the moment the first correction lands.
    /// </remarks>
    public int OriginalPoints { get; set; }

    /// <summary>
    /// Moderator who last changed the points (null while nobody has)
    /// </summary>
    /// <remarks>
    /// One correction, not a chain: the last one is what the log shows and what
    /// the scale counts. No cascade, because deleting the account of a moderator
    /// must not delete the warnings he corrected.
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
    /// Required of the change and shown wherever the warning is shown, the open
    /// log included: one moderator gave the warning and said why, another took
    /// the points off because it was no violation, and a reader who sees only
    /// the first sentence reads a verdict that no longer stands.
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
