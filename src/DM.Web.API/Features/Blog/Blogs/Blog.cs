using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Statuses;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
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

    /// <summary>
    /// Whether the viewer is on the blog's blacklist. False for a guest.
    /// </summary>
    /// <remarks>
    /// Lets the page leave out the edit of one's own comment, in the blog and
    /// under its publications, instead of refusing the text after it was
    /// written. The server asks the same rule on the comments it closes
    /// (BlogRoleExtensions.IsBlacklisted), so the flag decides what is offered,
    /// never what is allowed. The comment form goes by <see cref="Viewer" />,
    /// which answers for every reason at once.
    /// </remarks>
    public bool ViewerBlacklisted { get; set; }

    /// <summary>
    /// What the viewer may do in this blog. Null in a list of blogs: it is
    /// answered on a read of one blog and on every write that returns one, the
    /// shape a page offers something from
    /// </summary>
    public BlogViewer? Viewer { get; set; }
}

/// <summary>
/// What the viewer may do in a blog. Each flag is asked by the same code the
/// write it stands for is refused by, so a page that offers by it offers only
/// what the server takes; the server refuses all the same.
/// </summary>
public class BlogViewer
{
    /// <summary>
    /// Whether the viewer may write a new comment in the blog's discussion:
    /// comments on, the draft open to the viewer, the blog's blacklist and the
    /// ban, all at once. False for a guest
    /// </summary>
    public bool CanComment { get; set; }
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
    /// Copy the creator's personal blacklist into the blog's. Absent, the
    /// creator's account setting "Переносить черный список в новые игры и
    /// блоги" decides.
    /// </summary>
    public bool? CopyBlacklist { get; set; }
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
}

/// <summary>
/// Request body for a blog premoderation transition
/// </summary>
public class BlogPremoderationChangeRequest
{
    /// <summary>
    /// Requested premoderation transition (SetApproved / SetAwaitingEdits by a
    /// mentor, SubmitForApproval by the owner)
    /// </summary>
    public ModulePremoderationTransition Transition { get; set; }
}

/// <summary>
/// Request body for a blog status transition
/// </summary>
public class BlogStatusChangeRequest
{
    /// <summary>
    /// Requested status transition (Start / Freeze / Finish / Close / Reopen)
    /// </summary>
    public ModuleStatusTransition Transition { get; set; }
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
