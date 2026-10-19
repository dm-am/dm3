using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

public class UpdateChat
{
    public Guid ChatId { get; set; }

    /// <summary>
    /// New title, null keeps the current one
    /// </summary>
    public string? Title { get; set; }

    public IEnumerable<Guid>? AddParticipants { get; set; }

    public IEnumerable<Guid>? RemoveParticipants { get; set; }
}
