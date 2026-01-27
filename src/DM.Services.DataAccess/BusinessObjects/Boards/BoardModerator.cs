using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Boards;

/// <summary>
/// DAL model for board moderator
/// </summary>
[Table("BoardModerators")]
public class BoardModerator
{
    /// <summary>
    /// Board moderator identifier
    /// </summary>
    [Key]
    public Guid BoardModeratorId { get; set; }

    /// <summary>
    /// Board identifier
    /// </summary>
    public Guid BoardId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Board
    /// </summary>
    [ForeignKey(nameof(BoardId))]
    public virtual Board Board { get; set; }

    /// <summary>
    /// User
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
}
