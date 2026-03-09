using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for creating a group chat
/// </summary>
public class CreateChat
{
    /// <summary>
    /// Chat title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// List of participant user IDs (not including creator)
    /// </summary>
    public IEnumerable<Guid> ParticipantIds { get; set; } = [];
}
