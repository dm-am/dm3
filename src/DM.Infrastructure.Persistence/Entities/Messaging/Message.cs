using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for unified message (both global chat and private chats)
/// </summary>
[Table("Messages")]
public class Message : ISoftDeletable
{

    [Key]
    public Guid MessageId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Chat identifier (WellKnownChats.GlobalChatId for global chat)
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Text content
    /// </summary>
    public string Text { get; set; } = null!;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <summary>
    /// User who deleted the message (for moderation)
    /// </summary>
    public Guid? DeletedByUserId { get; set; }

    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Global chat event identifier (for messages sent during an event in global chat).
    /// Null for messages outside of events.
    /// </summary>
    public Guid? GlobalChatEventId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the message
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    [ForeignKey(nameof(ChatId))]
    public virtual Chat Chat { get; set; } = null!;

    /// <summary>
    /// Global chat event (if message was sent during an event)
    /// </summary>
    [ForeignKey(nameof(GlobalChatEventId))]
    public virtual GlobalChatEvent? GlobalChatEvent { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(MessageEdit.Message))]
    public virtual ICollection<MessageEdit> Edits { get; set; } = [];
}
