namespace DM.Domain.Core.Comments;

/// <summary>
/// Request to create a comment
/// </summary>
public class CreateComment
{
    /// <summary>
    /// Parent entity identifier (game, blog, publication, or topic)
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Comment text (BBCode)
    /// </summary>
    public string Text { get; set; } = null!;
}
