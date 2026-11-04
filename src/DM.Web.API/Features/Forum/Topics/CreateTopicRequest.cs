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
    // {1} is the maximum the attribute is given, so the number is written once.
    [StringLength(TopicPolicy.TitleMaxLength, ErrorMessage = "Заголовок должен быть от 1 до {1} символов")]
    public string Title { get; set; } = "";

    /// <summary>
    /// Topic description/content (BB-code formatted text)
    /// </summary>
    [Required(ErrorMessage = "Текст обязателен")]
    public string Text { get; set; } = "";
}
