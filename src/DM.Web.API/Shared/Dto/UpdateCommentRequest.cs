using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// API DTO for editing an existing comment
/// </summary>
/// <remarks>
/// The four comment PATCH endpoints used to take the response DTO. The contract
/// therefore asked a client editing one line of text to assemble a Comment: an
/// author of 28 fields, a list of likers, timestamps — none of which affects the
/// result, and none marked required, so the document never said which of the six
/// fields actually mattered. On the server the defence against the rest was a
/// line of Ignore() per field in the mapping profile, which is the arrangement
/// that once demoted an NPC to a player character on an unrelated edit.
/// </remarks>
public class UpdateCommentRequest
{
    /// <summary>
    /// Comment text (BB-code formatted)
    /// </summary>
    [Required(ErrorMessage = "Введите текст")]
    [MinLength(1, ErrorMessage = "Введите текст")]
    public string Text { get; set; } = "";
}
