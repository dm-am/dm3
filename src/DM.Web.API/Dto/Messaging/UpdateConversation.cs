using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// API DTO for updating a conversation
/// </summary>
public class UpdateConversation
{
    /// <summary>
    /// New title (null to keep current)
    /// </summary>
    [StringLength(100, ErrorMessage = "Title must not exceed 100 characters")]
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
