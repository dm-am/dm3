using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

public class Board
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// URL-friendly alias (ASCII, lowercase, hyphens)
    /// </summary>
    public string Alias { get; set; } = null!;

    public string Description { get; set; } = null!;

    public BoardAccessPolicy CreateTopicPolicy { get; set; }

    public IEnumerable<Guid> ModeratorIds { get; set; } = [];

    public IEnumerable<GeneralUser> Moderators { get; set; } = [];

    public int TopicsCount { get; set; }

    public int CommentsCount { get; set; }

    public int UnreadTopicsCount { get; set; }

    /// <summary>
    /// Unread comments across all topics of the board
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    public BoardLastComment? LastComment { get; set; }

    public BoardLastTopic? LastTopic { get; set; }
}

public class BoardLastComment
{
    public Guid Id { get; set; }

    public Guid TopicId { get; set; }

    public string TopicTitle { get; set; } = null!;

    /// <summary>
    /// Topic number (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Null for deleted users
    /// </summary>
    public GeneralUser? Author { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

public class BoardLastTopic
{
    public Guid Id { get; set; }

    /// <summary>
    /// Topic number (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// Null for deleted users
    /// </summary>
    public GeneralUser? Author { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
