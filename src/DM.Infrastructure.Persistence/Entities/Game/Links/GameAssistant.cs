using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Links;

/// <summary>
/// DAL model for game assistant
/// </summary>
[Table("GameAssistants")]
public class GameAssistant
{
    /// <summary>
    /// Entry identifier
    /// </summary>
    [Key]
    public Guid GameAssistantId { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When user became assistant
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    #region Navigation Properties

    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    #endregion
}
