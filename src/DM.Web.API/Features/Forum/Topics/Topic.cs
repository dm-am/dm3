using System;
using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Boards;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// API DTO model for topic
/// </summary>
public class Topic
{
    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic number within board (for URL)
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

    /// <summary>
    /// Last modification moment
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public CommonBbText Description { get; set; } = null!;

    /// <summary>
    /// Attached (pinned to top)
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Sort order for attached topics (0 = first)
    /// </summary>
    public int? AttachOrder { get; set; }

    /// <summary>
    /// Closed (read-only)
    /// </summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// Last activity moment (last comment or topic creation)
    /// </summary>
    public DateTimeOffset LastActivityUtc { get; set; }

    /// <summary>
    /// Last comment
    /// </summary>
    public LastTopicComment LastComment { get; set; } = null!;

    /// <summary>
    /// Total comments count
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Number of unread comments
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Board (forum section)
    /// </summary>
    public Board Board { get; set; } = null!;

    /// <summary>
    /// Users who like this
    /// </summary>
    public IEnumerable<User> Likes { get; set; } = [];

    /// <summary>
    /// Total likes count. Populated in the list path so the topics table
    /// can show the column without hydrating each topic's full Likes list.
    /// </summary>
    public int LikesCount { get; set; }
}

/// <summary>
/// Last topic comment DTO
/// </summary>
public class LastTopicComment
{
    /// <summary>
    /// Comment ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;
}
