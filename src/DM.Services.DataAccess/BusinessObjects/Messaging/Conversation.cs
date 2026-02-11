using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.DataAccess.BusinessObjects.Messaging;

/// <summary>
/// DAL model for users conversation
/// </summary>
[Table("Conversations")]
public class Conversation
{
    /// <summary>
    /// Well-known ID for global chat conversation
    /// </summary>
    public static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Conversation identifier
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Conversation type (Direct, Group, or Global)
    /// </summary>
    public ConversationType Type { get; set; }

    /// <summary>
    /// Conversation title (for group conversations, null for direct)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Last message identifier
    /// </summary>
    public Guid? LastMessageId { get; set; }

    /// <summary>
    /// Links with conversation participants
    /// </summary>
    [InverseProperty(nameof(UserConversationLink.Conversation))]
    public virtual ICollection<UserConversationLink> UserLinks { get; set; } = [];

    /// <summary>
    /// Messages
    /// </summary>
    [InverseProperty(nameof(Message.Conversation))]
    public virtual ICollection<Message> Messages { get; set; } = [];

    /// <summary>
    /// Last message
    /// </summary>
    [ForeignKey(nameof(LastMessageId))]
    public virtual Message? LastMessage { get; set; }
}