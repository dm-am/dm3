using System;

namespace DM.Services.Game.BusinessProcesses.Posts.Reading;

/// <summary>
/// DTO for best post result
/// </summary>
public class BestPostResult
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Post text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = null!;

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string RoomTitle { get; set; } = null!;

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Post author login
    /// </summary>
    public string AuthorLogin { get; set; } = null!;

    /// <summary>
    /// Post rating
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Post creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
