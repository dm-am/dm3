using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using BlogPremoderationTransition = DM.Domain.Blog.Features.Blogs.BlogPremoderationTransition;
using BlogStatusTransition = DM.Domain.Blog.Features.Blogs.BlogStatusTransition;

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
    /// Blog description (BBCode, server-rendered to HTML)
    /// </summary>
    public CommonBbText Description { get; set; } = null!;

    /// <summary>
    /// Blog mentor / curator (newbie blogs). Lightweight reference; null when
    /// the blog has no assigned curator. Gates the blog notepad for the mentor.
    /// </summary>
    public UserRef? Mentor { get; set; }

    /// <summary>
    /// Draft visibility (Private = only roles, Public = visible to all)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Premoderation status (newbie blogs; state-gates the mentor
    /// premoderation actions on the moderation panel)
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

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

    /// <summary>
    /// Total publication count in this rubric (a supplementary total, not
    /// part of the "(N/A)" counter)
    /// </summary>
    public int PublicationCount { get; set; }

    /// <summary>
    /// Unread publication count for the current user (the "N" in "(N/A)",
    /// doc 4.2.1.4)
    /// </summary>
    public int UnreadPublicationsCount { get; set; }

    /// <summary>
    /// Unread comment count across the rubric's publications for the current
    /// user (the "A" in "(N/A)", doc 4.2.1.4)
    /// </summary>
    public int UnreadCommentsCount { get; set; }
}

/// <summary>
/// Request to create a new blog
/// </summary>
public class CreateBlogRequest
{
    /// <summary>
    /// Blog title (1-200 characters)
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 200 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Blog description (BBCode)
    /// </summary>
    [StringLength(5000, ErrorMessage = "Описание не должно превышать 5000 символов")]
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
    /// Blog title (1-200 characters, optional)
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 200 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Blog description (BBCode, optional)
    /// </summary>
    [StringLength(5000, ErrorMessage = "Описание не должно превышать 5000 символов")]
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
/// Request body for a blog premoderation transition
/// </summary>
public class BlogPremoderationChangeRequest
{
    /// <summary>
    /// Requested premoderation transition (SendToPremoderation / RemoveFromPremoderation)
    /// </summary>
    public BlogPremoderationTransition Transition { get; set; }
}

/// <summary>
/// Request body for a blog status transition
/// </summary>
public class BlogStatusChangeRequest
{
    /// <summary>
    /// Requested status transition (Start / Freeze / Finish / Close / Reopen)
    /// </summary>
    public BlogStatusTransition Transition { get; set; }
}

/// <summary>
/// Request to create a new rubric
/// </summary>
public class CreateRubricRequest
{
    /// <summary>
    /// Rubric title (1-100 characters)
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 100 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Sort order (optional)
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Request to rename a rubric
/// </summary>
public class UpdateRubricRequest
{
    /// <summary>
    /// New rubric title (1-100 characters)
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Заголовок должен быть от 1 до 100 символов")]
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Request to reorder the rubrics of a blog
/// </summary>
public class ReorderRubricsRequest
{
    /// <summary>
    /// Rubric identifiers in the desired display order. The position in this
    /// list becomes the rubric's sort order.
    /// </summary>
    [Required(ErrorMessage = "Список рубрик обязателен")]
    public IReadOnlyList<Guid> RubricIds { get; set; } = [];
}
