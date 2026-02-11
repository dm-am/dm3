using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Blogs;

namespace DM.Services.Community.BusinessProcesses.Blogs.Reading;

/// <summary>
/// DTO for blog
/// </summary>
public class Blog
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Blog owner
    /// </summary>
    public GeneralUser Owner { get; set; } = null!;

    /// <summary>
    /// Blog mentor (for newbie blogs)
    /// </summary>
    public GeneralUser? Mentor { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the blog is public
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Total publication count
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Total comments count (to blog + all publications)
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Blog rubrics (categories)
    /// </summary>
    public IEnumerable<Rubric> Rubrics { get; set; } = [];

    /// <summary>
    /// Blog participants (for authorization checks)
    /// </summary>
    public IEnumerable<BlogParticipantInfo> Participants { get; set; } = Array.Empty<BlogParticipantInfo>();
}

/// <summary>
/// Simplified blog participant info
/// </summary>
public class BlogParticipantInfo
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Participation role
    /// </summary>
    public BlogParticipation Role { get; set; }
}

/// <summary>
/// DTO for rubric
/// </summary>
public class Rubric
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}
