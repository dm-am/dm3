using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging.Messages;
using ConversationType = DM.Domain.Core.Enums.ChatType;

namespace DM.Web.API.Features.Messaging.Conversations;

/// <summary>
/// API DTO model for user conversation
/// </summary>
public class Conversation
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Conversation type (direct, group, or global)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConversationType Type { get; set; }

    /// <summary>
    /// Conversation title (for group conversations, null for direct)
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Conversation participants
    /// </summary>
    public IEnumerable<User> Participants { get; set; } = [];

    /// <summary>
    /// Last conversation message
    /// </summary>
    public Message LastMessage { get; set; } = null!;

    /// <summary>
    /// Number of unread conversation messages
    /// </summary>
    public int UnreadMessagesCount { get; set; }
}
