using System;
using System.Collections.Generic;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <summary>
/// DTO for creating a group conversation
/// </summary>
public class CreateConversation
{
    /// <summary>
    /// Conversation title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// List of participant user IDs (not including creator)
    /// </summary>
    public IEnumerable<Guid> ParticipantIds { get; set; } = [];
}
