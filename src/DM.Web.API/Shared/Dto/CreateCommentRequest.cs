using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// API DTO for creating a new comment
/// </summary>
public class CreateCommentRequest
{
    /// <summary>
    /// Comment text (BB-code formatted)
    /// </summary>
    [Required(ErrorMessage = "Введите текст")]
    [MinLength(1, ErrorMessage = "Введите текст")]
    public string Text { get; set; } = "";
}
