namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// DTO model for creating new topic
/// </summary>
public class CreateTopic
{
    /// <summary>
    /// Parent board title
    /// </summary>
    public string BoardTitle { get; set; } = null!;

    /// <summary>
    /// New topic title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// New topic description text
    /// </summary>
    public string Text { get; set; } = null!;
}
