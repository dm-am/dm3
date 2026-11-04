using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

public class CreateBlogEntity
{
    public Guid BlogId { get; set; }

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Premoderation status the blog is born in — Approved for most authors,
    /// AwaitingEdits for a newbie or an author under moderation watch.
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    public DraftVisibility DraftVisibility { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateBlogEntity
{
    public Guid BlogId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DraftVisibility? DraftVisibility { get; set; }

    public ModuleStatus? Status { get; set; }

    public ClosedReason? ClosedReason { get; set; }

    /// <summary>
    /// Set on first activation
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Whether to clear ClosedUtc (when reopening)
    /// </summary>
    public bool ClearClosedUtc { get; set; }

    public PremoderationStatus? PremoderationStatus { get; set; }

    /// <summary>
    /// Mentor user id (only applied when <see cref="SetMentorId"/> is true;
    /// null clears the mentor). Kept separate from the nullable value so that the
    /// repository can distinguish "leave the current mentor untouched" from
    /// "explicitly set the mentor to null".
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Whether <see cref="MentorId"/> should be written (set or cleared).
    /// </summary>
    public bool SetMentorId { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }
}

public class CreateRubricEntity
{
    public Guid RubricId { get; set; }

    public Guid BlogId { get; set; }

    public string Title { get; set; } = null!;

    public int SortOrder { get; set; }
}

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdateRubricEntity
{
    public Guid RubricId { get; set; }

    public string? Title { get; set; }

    public int? SortOrder { get; set; }
}

public class AddBlogAssistantEntity
{
    public Guid BlogId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
