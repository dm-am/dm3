using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for blog publication
/// </summary>
public class Publication : ILikable
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc />
    public LikeEntityType LikeEntityType => LikeEntityType.Publication;

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Parent blog title (projected via join — lets callers show the blog
    /// name instead of a bare blog id, e.g. the profile "best publication").
    /// </summary>
    public string BlogTitle { get; set; } = null!;

    /// <summary>
    /// Rubric (optional)
    /// </summary>
    public Rubric Rubric { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Publication title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content (HTML)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string Preview { get; set; } = null!;

    /// <summary>
    /// Creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Whether the publication is published (visible)
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication date (UTC)
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// View count
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Comment count
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Unread comments count
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Last comment identifier for navigation
    /// </summary>
    public Guid? LastCommentId { get; set; }

    /// <summary>
    /// Users who liked this publication
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; } = [];
}
