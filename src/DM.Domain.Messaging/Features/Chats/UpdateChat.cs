using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for updating chat
/// </summary>
public class UpdateChat
{
    /// <summary>
    /// Chat identifier
    /// </summary>
    public Guid ChatId { get; set; }

    /// <summary>
    /// New title (null to keep current)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// User IDs to add as participants
    /// </summary>
    public IEnumerable<Guid>? AddParticipants { get; set; }

    /// <summary>
    /// User IDs to remove from participants
    /// </summary>
    public IEnumerable<Guid>? RemoveParticipants { get; set; }
}
