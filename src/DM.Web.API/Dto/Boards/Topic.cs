using System;
using System.Collections.Generic;
using DM.Web.API.BbRendering;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Boards;

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
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last edit moment
    /// </summary>
    public DateTimeOffset? EditedUtc { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Description
    /// </summary>
    public CommonBbText Description { get; set; } = null!;

    /// <summary>
    /// Attached (pinned to top)
    /// </summary>
    public bool? IsAttached { get; set; }

    /// <summary>
    /// Closed (read-only)
    /// </summary>
    public bool? IsClosed { get; set; }

    /// <summary>
    /// Last commentary
    /// </summary>
    public LastTopicComment LastComment { get; set; } = null!;

    /// <summary>
    /// Total commentaries count
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Number of unread commentaries
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
}

/// <summary>
/// Last topic commentary DTO
/// </summary>
public class LastTopicComment
{
    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;
}