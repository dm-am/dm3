using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Messaging;

/// <summary>
/// API DTO for creating a group conversation
/// </summary>
public class CreateConversation
{
    /// <summary>
    /// Conversation title
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 100 characters")]
    public string Title { get; set; } = "";

    /// <summary>
    /// List of participant user IDs (not including creator)
    /// </summary>
    [Required(ErrorMessage = "At least one participant is required")]
    [MinLength(1, ErrorMessage = "At least one participant is required")]
    public IEnumerable<Guid> ParticipantIds { get; set; } = [];
}
