using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// Board DTO model
/// </summary>
public class Board
{
    /// <summary>
    /// Board identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Board title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// URL-friendly alias (ASCII, lowercase, hyphens)
    /// </summary>
    public string Alias { get; set; } = null!;

    /// <summary>
    /// Short description
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Create topic policy
    /// </summary>
    public BoardAccessPolicy CreateTopicPolicy { get; set; }

    /// <summary>
    /// View topic policy
    /// </summary>
    public BoardAccessPolicy ViewPolicy { get; set; }

    /// <summary>
    /// Moderator identifiers
    /// </summary>
    public IEnumerable<Guid> ModeratorIds { get; set; } = [];

    /// <summary>
    /// Board moderators
    /// </summary>
    public IEnumerable<GeneralUser> Moderators { get; set; } = [];

    /// <summary>
    /// Total number of topics in the forum
    /// </summary>
    public int TopicsCount { get; set; }

    /// <summary>
    /// Total number of comments in the forum
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Total number of board topics that has unread comments
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
/// Last comment in board DTO model
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
    public string TopicTitle { get; set; } = null!;

    /// <summary>
    /// Topic number (for URL)
    /// </summary>
    public int TopicNumber { get; set; }

    /// <summary>
    /// Author (may be null for deleted users)
    /// </summary>
    public GeneralUser? Author { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Last created topic in board DTO model
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
    public string Title { get; set; } = null!;

    /// <summary>
    /// Author (may be null for deleted users)
    /// </summary>
    public GeneralUser? Author { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
