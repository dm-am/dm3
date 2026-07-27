using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.Blog.Publications;

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
    /// Parent blog title (so clients can show the blog name instead of its id)
    /// </summary>
    public string BlogTitle { get; set; } = string.Empty;

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
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Publication content (BBCode, server-rendered to HTML)
    /// </summary>
    public CommonBbText Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string Preview { get; set; } = string.Empty;

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
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 300 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Publication content (BBCode)
    /// </summary>
    [Required(ErrorMessage = "Содержимое обязательно")]
    [StringLength(100000, MinimumLength = 1, ErrorMessage = "Содержимое должно быть от 1 до 100000 символов")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Short preview/excerpt (max 500 characters)
    /// </summary>
    [StringLength(500, ErrorMessage = "Превью не должно превышать 500 символов")]
    public string Preview { get; set; } = string.Empty;

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
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 300 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Publication content (BBCode, optional)
    /// </summary>
    [StringLength(100000, ErrorMessage = "Содержимое не должно превышать 100000 символов")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Short preview/excerpt (max 500 characters, optional)
    /// </summary>
    [StringLength(500, ErrorMessage = "Превью не должно превышать 500 символов")]
    public string Preview { get; set; } = string.Empty;

    /// <summary>
    /// Whether the publication is published (optional)
    /// </summary>
    public bool? IsPublished { get; set; }

    /// <summary>
    /// Whether comments are enabled (optional)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}
