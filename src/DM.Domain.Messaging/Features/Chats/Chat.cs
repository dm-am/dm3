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
    /// Room this chat belongs to, for a game room chat; null for every other type
    /// </summary>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// Identifier this chat is counted under in unread markers
    /// </summary>
    /// <remarks>
    /// A game room chat is one thing with two identifiers: the game module knows
    /// it as a room and the messaging module as a chat. The unread marker is
    /// created, aggregated into the game badge and deleted by the room half, so
    /// the room identifier is the one that exists — counting messages under the
    /// chat identifier wrote into markers nobody had made and read back zero
    /// forever. Every other chat is only ever itself.
    /// </remarks>
    public Guid UnreadEntityId => RoomId ?? Id;

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
    /// Moment the reader cleared the history of this chat for himself: messages
    /// created at or before it are not given to him, and <see cref="LastMessage"/>
    /// is null when it is one of them. Null when he never cleared it, and on a
    /// read made for nobody in particular.
    /// </summary>
    public DateTimeOffset? HistoryClearedUtc { get; set; }
}
