using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Forum;

/// <summary>
/// DAL model for board moderator
/// </summary>
[Table("BoardModerators")]
public class BoardModerator
{
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

    [ForeignKey(nameof(BoardId))]
    public virtual Board Board { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
