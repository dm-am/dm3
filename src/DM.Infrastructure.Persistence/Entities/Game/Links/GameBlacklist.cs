using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Links;

/// <summary>
/// DAL model for game blacklist entry (user banned from participating in a game)
/// </summary>
[Table("GameBlacklists")]
public class GameBlacklist
{
    [Key]
    public Guid EntryId { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// Blocked user identifier
    /// </summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>
    /// When user was blocked
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Who blocked the user
    /// </summary>
    public Guid BlockedByUserId { get; set; }

    #region Navigation Properties

    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey(nameof(BlockedUserId))]
    public virtual User BlockedUser { get; set; } = null!;

    [ForeignKey(nameof(BlockedByUserId))]
    public virtual User BlockedBy { get; set; } = null!;

    #endregion
}
