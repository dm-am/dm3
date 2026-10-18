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

    /// <summary>
    /// What the viewer may do in this chat. Null in the list of chats: it is
    /// answered on a read of one chat and on every write that returns one, the
    /// shape the page of a correspondence offers its field from
    /// </summary>
    public ChatViewer? Viewer { get; set; }
}

/// <summary>
/// What the viewer may do in a chat. Each flag is asked by the same code the
/// write it stands for is refused by, so a page that offers by it offers only
/// what the server takes; the server refuses all the same.
/// </summary>
public class ChatViewer
{
    /// <summary>
    /// Whether the viewer may write a new message in the chat: a participant of
    /// the conversation, not closed off by the one other participant of a
    /// conversation of two; in the global chat, signed in, under no ban and not
    /// outside a closed event
    /// </summary>
    public bool CanWrite { get; set; }

    /// <summary>
    /// Whether the viewer may leave the chat: a participant of a group chat. A
    /// direct chat and the global chat are not left
    /// </summary>
    public bool CanLeave { get; set; }

    /// <summary>
    /// Whether the viewer may clear the history of the chat for himself: a
    /// participant of a direct or group chat. The others keep their history
    /// </summary>
    public bool CanClearHistory { get; set; }
}
