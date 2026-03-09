using System;

namespace DM.Domain.Game.Features.Comments;

/// <summary>
/// DTO for creating a game comment
/// </summary>
public class CreateGameComment
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Comment author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// New comment count for the game
    /// </summary>
    public int NewCommentCount { get; set; }
}
