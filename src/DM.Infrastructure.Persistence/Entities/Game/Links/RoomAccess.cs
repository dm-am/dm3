using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Game.Links;

/// <summary>
/// DAL model for room access control (character or reader user)
/// </summary>
[Table("RoomAccesses")]
public class RoomAccess
{
    /// <summary>
    /// Link identifier
    /// </summary>
    [Key]
    public Guid AccessId { get; set; }

    /// <summary>
    /// Character identifier (for player access)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Reader user identifier (for reader access, direct link to User)
    /// </summary>
    public Guid? ReaderUserId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }

    /// <summary>
    /// Character navigation
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character? Character { get; set; }

    /// <summary>
    /// Reader user navigation (direct link to User, no Reader table)
    /// </summary>
    [ForeignKey(nameof(ReaderUserId))]
    public virtual User? ReaderUser { get; set; }

    /// <summary>
    /// Room navigation
    /// </summary>
    [ForeignKey(nameof(RoomId))]
    public virtual Room Room { get; set; } = null!;
}
