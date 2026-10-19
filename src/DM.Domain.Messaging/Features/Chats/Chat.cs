using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;

namespace DM.Domain.Messaging.Features.Chats;

public class Chat
{
    public Guid Id { get; set; }

    public ChatType Type { get; set; }

    /// <summary>
    /// Chat title, null for a direct chat
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Room this chat belongs to, for a game room chat; null for every other type
    /// </summary>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// Identifier this chat is counted under in unread markers: the game module keeps
    /// the markers of a game room chat under its room
    /// </summary>
    public Guid UnreadEntityId => RoomId ?? Id;

    public IEnumerable<GeneralUser> Participants { get; set; } = [];

    /// <summary>
    /// Last chat message, null when there is none or the reader cleared it
    /// </summary>
    public Message LastMessage { get; set; } = null!;

    public int UnreadMessagesCount { get; set; }

    /// <summary>
    /// Moment the reader cleared the history of this chat for himself, null when he
    /// never did: messages created at or before it are not given to him.
    /// </summary>
    public DateTimeOffset? HistoryClearedUtc { get; set; }
}
