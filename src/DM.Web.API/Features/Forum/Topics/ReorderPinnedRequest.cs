using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// API DTO for reordering pinned topics
/// </summary>
public class ReorderPinnedRequest
{
    /// <summary>
    /// Topic IDs in desired order (first = top of list)
    /// </summary>
    [Required(ErrorMessage = "Укажите топики")]
    public Guid[] TopicIds { get; set; } = [];
}
