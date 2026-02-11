using System;
using System.Collections.Generic;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// DTO for updating conversation
/// </summary>
public class UpdateConversation
{
    /// <summary>
    /// Conversation identifier
    /// </summary>
    public Guid ConversationId { get; set; }

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
