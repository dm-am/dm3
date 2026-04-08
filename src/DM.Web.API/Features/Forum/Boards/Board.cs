using System;
using System.Collections.Generic;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// API DTO model for board (forum section)
/// </summary>
public class Board
{
    /// <summary>
    /// Board identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Board title (human-readable name)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly alias (ASCII, lowercase, hyphens)
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Short description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Board moderators
    /// </summary>
    public IEnumerable<UserRef> Moderators { get; set; } = [];

    /// <summary>
    /// Total number of topics in the board
    /// </summary>
    public int TopicsCount { get; set; }

    /// <summary>
    /// Total number of comments in the board
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Total count of topics with unread comments within
    /// </summary>
    public int UnreadTopicsCount { get; set; }

    /// <summary>
    /// Total number of unread comments across all topics
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Last comment in the board
    /// </summary>
    public BoardLastComment? LastComment { get; set; }

    /// <summary>
    /// Last created topic in the board
    /// </summary>
    public BoardLastTopic? LastTopic { get; set; }
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
    /// Topic title
    /// </summary>
    public string TopicTitle { get; set; } = string.Empty;

    /// <summary>
    /// Topic number (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Last created topic in board DTO
/// </summary>
public class BoardLastTopic
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic number (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Topic title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
