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

    /// <summary>
    /// What the viewer may do on this board. Null in the list of boards and in
    /// the topics of a board: it is answered on a read of one board and on the
    /// board of a topic read alone, the shapes the forum page offers a new topic
    /// from
    /// </summary>
    public BoardViewer? Viewer { get; set; }
}

/// <summary>
/// What the viewer may do on a board. Each flag is asked by the same code the
/// write it stands for is refused by, so a page that offers by it offers only
/// what the server takes; the server refuses all the same.
/// </summary>
public class BoardViewer
{
    /// <summary>
    /// Whether the viewer may open a topic on the board: the board's policy
    /// against his role, and the ban. False for a guest
    /// </summary>
    public bool CanCreateTopic { get; set; }

    /// <summary>
    /// Whether signing in is the way to a topic here: the viewer is a guest,
    /// and an ordinary member may open topics on the board. False for anybody
    /// signed in, and on a board whose topics come from the moderation
    /// </summary>
    public bool CanCreateTopicAfterSignIn { get; set; }
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
