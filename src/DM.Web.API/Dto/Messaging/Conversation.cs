using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Messaging;

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