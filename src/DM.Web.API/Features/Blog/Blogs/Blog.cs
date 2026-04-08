using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Blog.Blogs;

/// <summary>
/// Blog information (for lists and cards)
/// </summary>
/// <remarks>
/// Extends BlogRef with additional fields.
/// Inherits: Id, Title, Author, Assistants, Status, CreatedUtc, ActivatedUtc, ClosedUtc,
///           SubscribersCount, ActiveSubscribersCount, UnreadPublicationsCount, UnreadCommentsCount
/// </remarks>
public class Blog : BlogRef
{
    // Inherited from BlogRef:
    // Id, Title, Author, Assistants, Status, CreatedUtc, ActivatedUtc, ClosedUtc,
    // SubscribersCount, ActiveSubscribersCount, UnreadPublicationsCount, UnreadCommentsCount

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Draft visibility (Private = only roles, Public = visible to all)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Total publication count
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Total comments count across all publications
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Blog rubrics (categories)
    /// </summary>
    public IEnumerable<Rubric> Rubrics { get; set; } = [];
}

/// <summary>
/// Blog detailed information (for blog page)
/// </summary>
public class BlogDetails : Blog
{
    /// <summary>
    /// Blog subscribers (lightweight references)
    /// </summary>
    public IEnumerable<UserRef> Subscribers { get; set; } = [];

    /// <summary>
    /// Blog assistants (lightweight references)
    /// </summary>
    public IEnumerable<UserRef> FullAssistants { get; set; } = [];
}

/// <summary>
/// Blog rubric (category)
/// </summary>
public class Rubric
{
    /// <summary>
    /// Rubric unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Request to create a new blog
/// </summary>
public class CreateBlogRequest
{
    /// <summary>
    /// Blog title (1-200 символов)
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 and 200 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Draft visibility (default: Public)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; } = DraftVisibility.Public;

    /// <summary>
    /// Whether comments are enabled (default: true)
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;

    /// <summary>
    /// Copy personal blacklist to blog blacklist on creation
    /// </summary>
    public bool CopyBlacklist { get; set; }
}

/// <summary>
/// Request to update a blog
/// </summary>
public class UpdateBlogRequest
{
    /// <summary>
    /// Blog title (1-200 символов, optional)
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 and 200 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Blog description (optional)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Draft visibility (optional)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled (optional)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}

/// <summary>
/// Request to create a new rubric
/// </summary>
public class CreateRubricRequest
{
    /// <summary>
    /// Rubric title (1-100 символов)
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 and 100 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Sort order (optional)
    /// </summary>
    public int SortOrder { get; set; }
}
