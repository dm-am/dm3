using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// Service DTO for chat
/// </summary>
public class Chat
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Chat type (Direct, Group, or Global)
    /// </summary>
    public ChatType Type { get; set; }

    /// <summary>
    /// Chat title (for group chats, null for direct)
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// List of chat participants
    /// </summary>
    public IEnumerable<GeneralUser> Participants { get; set; } = [];

    /// <summary>
    /// Last chat message
    /// </summary>
    public Message LastMessage { get; set; } = null!;

    /// <summary>
    /// Number of unread messages
    /// </summary>
    public int UnreadMessagesCount { get; set; }

    /// <summary>
    /// Total number of messages
    /// </summary>
    public int TotalMessagesCount { get; set; }
}
