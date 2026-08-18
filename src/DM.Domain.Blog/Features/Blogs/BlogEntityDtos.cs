using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// Entity DTO for creating a blog (repository level)
/// </summary>
public class CreateBlogEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Owner user identifier
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Premoderation status the blog is born in — Approved for most authors,
    /// AwaitingEdits for a newbie or an author under moderation watch.
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// Draft visibility
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a blog (repository level)
/// </summary>
public class UpdateBlogEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// New title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New description (if changed)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// New visibility (if changed)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Comments enabled flag (if changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }

    /// <summary>
    /// Blog status (if changed)
    /// </summary>
    public ModuleStatus? Status { get; set; }

    /// <summary>
    /// Reason why the blog was closed (if changed)
    /// </summary>
    public ClosedReason? ClosedReason { get; set; }

    /// <summary>
    /// Activation timestamp (set on first activation)
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    /// <summary>
    /// Closed timestamp (set when closing)
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Whether to clear ClosedUtc (when reopening)
    /// </summary>
    public bool ClearClosedUtc { get; set; }

    /// <summary>
    /// Premoderation status (if changed)
    /// </summary>
    public PremoderationStatus? PremoderationStatus { get; set; }

    /// <summary>
    /// Curating mentor user id (only applied when <see cref="SetMentorId"/> is true;
    /// null clears the curator). Kept separate from the nullable value so that the
    /// repository can distinguish "leave the current mentor untouched" from
    /// "explicitly set the mentor to null".
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Whether <see cref="MentorId"/> should be written (set or cleared).
    /// </summary>
    public bool SetMentorId { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a rubric (repository level)
/// </summary>
public class CreateRubricEntity
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Entity DTO for updating a rubric (repository level)
/// </summary>
public class UpdateRubricEntity
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// New title (if changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// New sort order (if changed)
    /// </summary>
    public int? SortOrder { get; set; }
}

/// <summary>
/// Entity DTO for adding a blog assistant (repository level)
/// </summary>
public class AddBlogAssistantEntity
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Join timestamp
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }
}
