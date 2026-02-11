using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Blogs;

/// <summary>
/// Blog publication (post)
/// </summary>
public class Publication
{
    /// <summary>
    /// Publication unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric (category, optional)
    /// </summary>
    public Rubric Rubric { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    public User Author { get; set; } = null!;

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
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last modification date
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>
    /// Whether the publication is published (visible)
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication date
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

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
    /// Users who liked this publication
    /// </summary>
    public IEnumerable<User> Likes { get; set; } = [];
}

/// <summary>
/// Request to create a new publication
/// </summary>
public class CreatePublicationRequest
{
    /// <summary>
    /// Rubric identifier (optional)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Publication title (1-300 characters)
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 300 characters")]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content
    /// </summary>
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt (max 500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Preview must not exceed 500 characters")]
    public string Preview { get; set; } = null!;

    /// <summary>
    /// Whether to publish immediately (default: false - draft)
    /// </summary>
    public bool PublishImmediately { get; set; }

    /// <summary>
    /// Whether comments are enabled (default: true)
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;
}

/// <summary>
/// Request to update a publication
/// </summary>
public class UpdatePublicationRequest
{
    /// <summary>
    /// Rubric identifier (optional, null to keep, use ClearRubric to remove)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Whether to clear the rubric
    /// </summary>
    public bool ClearRubric { get; set; }

    /// <summary>
    /// Publication title (1-300 characters, optional)
    /// </summary>
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 300 characters")]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content (optional)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt (max 500 characters, optional)
    /// </summary>
    [StringLength(500, ErrorMessage = "Preview must not exceed 500 characters")]
    public string Preview { get; set; } = null!;

    /// <summary>
    /// Whether the publication is published (optional)
    /// </summary>
    public bool? IsPublished { get; set; }

    /// <summary>
    /// Whether comments are enabled (optional)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}
