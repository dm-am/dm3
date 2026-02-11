using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Games.Links;

/// <summary>
/// DAL model for game reader
/// </summary>
[Table("Readers")]
public class Reader
{
    /// <summary>
    /// Link identifier
    /// </summary>
    [Key]
    public Guid ReaderId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Reader identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Game
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Reader
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Room links
    /// </summary>
    [InverseProperty(nameof(RoomAccess.Reader))]
    public virtual ICollection<RoomAccess> RoomLinks { get; set; } = [];
}