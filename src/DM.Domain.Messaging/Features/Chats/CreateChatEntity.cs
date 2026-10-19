using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.Chats;

public class CreateChatEntity
{
    public Guid ChatId { get; set; }

    public ChatType Type { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// Room of a game room chat
    /// </summary>
    public Guid? RoomId { get; set; }
}
