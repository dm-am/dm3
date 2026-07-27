using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL model for user endorsement (positive recommendation between users)
/// </summary>
/// <remarks>
/// The Text field is plain text (owner decision) - no BBCode rendering.
/// Only positive endorsements are allowed - this is a recommendation system.
/// One endorsement per author-target pair.
/// </remarks>
[Table("UserEndorsements")]
public class UserEndorsement : ISoftDeletable
{
    /// <summary>
    /// Endorsement identifier
    /// </summary>
    [Key]
    public Guid UserEndorsementId { get; set; }

    /// <summary>
    /// Author identifier (user giving the endorsement)
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Target user identifier (user receiving the endorsement)
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Endorsement text (plain text)
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Author (user giving the endorsement)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Target user (user receiving the endorsement)
    /// </summary>
    [ForeignKey(nameof(TargetUserId))]
    public virtual User TargetUser { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the endorsement
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }
}
