using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Boards;

/// <summary>
/// API DTO for creating a new topic
/// </summary>
public class CreateTopicRequest
{
    /// <summary>
    /// Topic title
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string Title { get; set; } = "";

    /// <summary>
    /// Topic description/content (BB-code formatted text)
    /// </summary>
    [Required(ErrorMessage = "Text is required")]
    [MinLength(1, ErrorMessage = "Text cannot be empty")]
    public string Text { get; set; } = "";
}
