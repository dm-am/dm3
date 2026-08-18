using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model for user warning
/// </summary>
[Table("Warnings")]
public class Warning : IAdministrated, IRemovable
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
    /// Warning points based on the violation
    /// </summary>
    public int Points { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    [ForeignKey(nameof(TargetUserId))]
    public virtual User TargetUser { get; set; } = null!;

    /// <inheritdoc />
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;
}
