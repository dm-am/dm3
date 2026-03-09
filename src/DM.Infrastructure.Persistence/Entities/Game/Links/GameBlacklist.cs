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
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    [Column("GameBlacklistId")]
    public Guid EntryId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
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

    /// <summary>
    /// Game
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Blocked user
    /// </summary>
    [ForeignKey(nameof(BlockedUserId))]
    public virtual User BlockedUser { get; set; } = null!;

    /// <summary>
    /// User who blocked
    /// </summary>
    [ForeignKey(nameof(BlockedByUserId))]
    public virtual User BlockedBy { get; set; } = null!;

    #endregion
}
