using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging.Messages;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// API DTO model for user chat
/// </summary>
public class Chat
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Chat type (direct, group, or global)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChatType Type { get; set; }

    /// <summary>
    /// Chat title (for group chats, null for direct)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Chat participants
    /// </summary>
    public IEnumerable<User> Participants { get; set; } = [];

    /// <summary>
    /// Last chat message
    /// </summary>
    public Message LastMessage { get; set; } = null!;

    /// <summary>
    /// Number of unread chat messages
    /// </summary>
    public int UnreadMessagesCount { get; set; }
}
