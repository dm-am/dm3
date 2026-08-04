using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Configuration;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// API DTO for creating a new topic
/// </summary>
public class CreateTopicRequest
{
    /// <summary>
    /// Topic title
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(TopicPolicy.TitleMaxLength, MinimumLength = 3,
        ErrorMessage = "Заголовок должен быть от 3 до 130 символов")]
    public string Title { get; set; } = "";

    /// <summary>
    /// Topic description/content (BB-code formatted text)
    /// </summary>
    [Required(ErrorMessage = "Текст обязателен")]
    [MinLength(1, ErrorMessage = "Текст не может быть пустым")]
    public string Text { get; set; } = "";
}
