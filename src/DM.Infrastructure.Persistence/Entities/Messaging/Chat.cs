using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Messaging;

/// <summary>
/// DAL model for users chat
/// </summary>
[Table("Chats")]
public class Chat
{
    /// <summary>
    /// Well-known ID for global chat
    /// </summary>
    public static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// Chat type (Direct, Group, or Global)
    /// </summary>
    public ChatType Type { get; set; }

    /// <summary>
    /// Chat title (for group chats, null for direct)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Last message identifier
    /// </summary>
    public Guid? LastMessageId { get; set; }

    /// <summary>
    /// Links with chat participants
    /// </summary>
    [InverseProperty(nameof(UserChatLink.Chat))]
    public virtual ICollection<UserChatLink> UserLinks { get; set; } = [];

    /// <summary>
    /// Messages
    /// </summary>
    [InverseProperty(nameof(Message.Chat))]
    public virtual ICollection<Message> Messages { get; set; } = [];

    /// <summary>
    /// Last message
    /// </summary>
    [ForeignKey(nameof(LastMessageId))]
    public virtual Message? LastMessage { get; set; }
}
