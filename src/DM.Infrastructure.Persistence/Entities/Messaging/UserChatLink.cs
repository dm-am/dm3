using System;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for chat participant
/// </summary>
[Table("UserChatLinks")]
public class UserChatLink : ISoftDeletable
{
    /// <summary>
    /// Link identifier
    /// </summary>
    public Guid UserChatLinkId { get; set; }

    /// <summary>
    /// Participant identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Participant
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Chat
    /// </summary>
    [ForeignKey(nameof(ChatId))]
    public virtual Chat Chat { get; set; } = null!;

    /// <summary>
    /// User who removed the link
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }
}
