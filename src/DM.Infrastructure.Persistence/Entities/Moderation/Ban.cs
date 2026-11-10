using DM.Domain.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model for user ban.
/// </summary>
/// <remarks>
/// Not soft-deletable: a lifted ban keeps its row for the moderation history and
/// is marked by <see cref="LiftedUtc" />. It stays off IRemovable on purpose:
/// under the global IsRemoved filter of DmDbContext a lifted ban would leave every
/// history the moment it was lifted.
/// </remarks>
[Table("Bans")]
public class Ban
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    [Key]
    public Guid BanId { get; set; }

    /// <summary>
    /// User who received the ban
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Moderator who issued the ban
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Moment from (UTC)
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Moment to (UTC)
    /// </summary>
    public DateTimeOffset EndedUtc { get; set; }

    /// <summary>
    /// Moderator comment for the ban
    /// </summary>
    public string Comment { get; set; } = null!;

    /// <summary>
    /// Restriction policy for banned user
    /// </summary>
    public AccessPolicy AccessRestrictionPolicy { get; set; }

    /// <summary>
    /// Flag that displays that user asked to be banned
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// Moderator who lifted the ban early (null while the ban runs its course)
    /// </summary>
    public Guid? LiftedByUserId { get; set; }

    /// <summary>
    /// When the ban was lifted early
    /// </summary>
    public DateTimeOffset? LiftedUtc { get; set; }

    /// <summary>
    /// Why the ban was lifted early
    /// </summary>
    public string? LiftReason { get; set; }

    [ForeignKey(nameof(TargetUserId))]
    public virtual User TargetUser { get; set; } = null!;

    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;
}
