using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Administration;

/// <summary>
/// DAL model for user warning
/// </summary>
[Table("Warnings")]
public class Warning : IAdministrated
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    [Key]
    public Guid WarningId { get; set; }

    /// <inheritdoc />
    public Guid UserId { get; set; }

    /// <inheritdoc />
    public Guid ModeratorId { get; set; }

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
    /// Warning points based on the violation
    /// </summary>
    public int Points { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <inheritdoc />
    [ForeignKey(nameof(ModeratorId))]
    public virtual User Moderator { get; set; } = null!;
}