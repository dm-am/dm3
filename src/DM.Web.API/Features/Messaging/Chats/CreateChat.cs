using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// API DTO for creating a group chat
/// </summary>
public class CreateChat
{
    /// <summary>
    /// Chat title
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 and 100 символов")]
    public string Title { get; set; } = "";

    /// <summary>
    /// List of participant user IDs (not including creator)
    /// </summary>
    [Required(ErrorMessage = "Минимум один участник")]
    [MinLength(1, ErrorMessage = "Минимум один участник")]
    public IEnumerable<Guid> ParticipantIds { get; set; } = [];
}
