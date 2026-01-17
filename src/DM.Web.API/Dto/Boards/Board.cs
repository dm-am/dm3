using System;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Boards;

/// <summary>
/// API DTO model for board (forum section)
/// </summary>
public class Board
{
    /// <summary>
    /// Board identifier (title)
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Short description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Total number of topics in the board
    /// </summary>
    public int TopicsCount { get; set; }

    /// <summary>
    /// Total number of comments in the board
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Total count of topics with unread commentaries within
    /// </summary>
    public int UnreadTopicsCount { get; set; }

    /// <summary>
    /// Total number of unread commentaries across all topics
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Last comment in the board
    /// </summary>
    public BoardLastComment LastComment { get; set; }
}

/// <summary>
/// Last comment in board DTO
/// </summary>
public class BoardLastComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
