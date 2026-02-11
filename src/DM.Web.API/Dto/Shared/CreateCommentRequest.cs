using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Shared;

/// <summary>
/// API DTO for creating a new comment
/// </summary>
public class CreateCommentRequest
{
    /// <summary>
    /// Comment text (BB-code formatted)
    /// </summary>
    [Required(ErrorMessage = "Text is required")]
    [MinLength(1, ErrorMessage = "Text cannot be empty")]
    public string Text { get; set; } = "";
}
