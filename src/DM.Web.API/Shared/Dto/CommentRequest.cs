using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// API DTO for writing or editing a comment
/// </summary>
/// <remarks>
/// A request of its own, not the response DTO: a comment carries the text and nothing else.
/// </remarks>
public class CommentRequest
{
    /// <summary>
    /// Comment text (BB-code formatted)
    /// </summary>
    [Required(ErrorMessage = "Введите текст")]
    public string Text { get; set; } = "";
}
