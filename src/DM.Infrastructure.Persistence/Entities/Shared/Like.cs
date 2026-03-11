using DM.Domain.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for like
/// </summary>
[Table("Likes")]
public class Like : IRemovable
{
    /// <summary>
    /// Removed flag
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Like identifier
    /// </summary>
    [Key]
    public Guid LikeId { get; set; }

    /// <summary>
    /// Parent entity identifier (e.g. comment, topic, etc.)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Type of the parent entity
    /// </summary>
    public LikeEntityType EntityType { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
