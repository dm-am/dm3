using System;
using System.Collections.Generic;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// API DTO for creating a group conversation
/// </summary>
public class CreateConversation
{
    /// <summary>
    /// Conversation title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// List of participant user IDs (not including creator)
    /// </summary>
    public IEnumerable<Guid> ParticipantIds { get; set; }
}
