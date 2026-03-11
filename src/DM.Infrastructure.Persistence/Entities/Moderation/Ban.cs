using DM.Domain.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model for user ban
/// </summary>
[Table("Bans")]
public class Ban : IAdministrated
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    [Key]
    public Guid BanId { get; set; }

    /// <inheritdoc />
    public Guid TargetUserId { get; set; }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    [ForeignKey(nameof(TargetUserId))]
    public virtual User TargetUser { get; set; } = null!;

    /// <inheritdoc />
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;
}