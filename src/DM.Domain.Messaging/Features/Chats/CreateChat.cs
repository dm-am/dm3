using System;
using System.Collections.Generic;

namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// DTO for creating a group chat
/// </summary>
public class CreateChat
{
    public string Title { get; set; } = null!;

    /// <summary>
    /// Participants, the creator not included
    /// </summary>
    public IEnumerable<Guid> ParticipantIds { get; set; } = [];
}
