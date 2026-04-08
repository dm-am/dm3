using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for authorization token
/// </summary>
public class Token : ISoftDeletable
{
    /// <summary>
    /// Token identifier
    /// </summary>
    [Key]
    public Guid TokenId { get; set; }

    /// <summary>
    /// Authorised user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Related entity identifier
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Authorised action type
    /// </summary>
    public TokenType Type { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// User who created this token (for invitations)
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// Authorised user
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// User who created this token
    /// </summary>
    [ForeignKey(nameof(CreatorId))]
    public virtual User? Creator { get; set; }

    /// <summary>
    /// User who deleted this token
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Related game
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Game.Game? Game { get; set; }

    /// <summary>
    /// Related blog
    /// </summary>
    [ForeignKey(nameof(EntityId))]
    public virtual Blog.Blog? Blog { get; set; }
}
