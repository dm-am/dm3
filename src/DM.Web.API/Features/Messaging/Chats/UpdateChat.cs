using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// API DTO for updating a chat
/// </summary>
public class UpdateChat
{
    /// <summary>
    /// New title (null to keep current)
    /// </summary>
    [StringLength(100, ErrorMessage = "Заголовок не должен превышать 100 символов")]
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
