namespace DM.Domain.Blog.Authorization;

/// <summary>
/// Blog-related intentions
/// </summary>
public enum BlogIntention
{
    /// <summary>
    /// Create a new blog
    /// </summary>
    Create,

    /// <summary>
    /// Owner-level blog editing (assistant removal, blacklist management)
    /// </summary>
    Edit,

    /// <summary>
    /// Delete a blog
    /// </summary>
    Delete,

    /// <summary>
    /// Create rubric in blog
    /// </summary>
    CreateRubric,

    /// <summary>
    /// Create publication in blog
    /// </summary>
    CreatePublication,

    /// <summary>
    /// View draft blog
    /// </summary>
    ViewDraft,

    /// <summary>
    /// Invite assistant to blog (owner only)
    /// </summary>
    InviteAssistant,

    /// <summary>
    /// Invite reader to blog (owner or assistant)
    /// </summary>
    InviteReader,

    /// <summary>
    /// Cancel invitation (owner only, creator check in service)
    /// </summary>
    CancelInvitation,

    /// <summary>
    /// Approve or reject publications (for mentors)
    /// </summary>
    ApprovePublications,

    /// <summary>
    /// Assign a mentor to the blog
    /// </summary>
    AssignMentor,

    /// <summary>
    /// Create comment on blog
    /// </summary>
    CreateComment,

    /// <summary>
    /// Handle premoderation (mentor approval/rejection)
    /// </summary>
    SetStatusModeration,

    /// <summary>
    /// View a blog that is pending premoderation (not yet approved)
    /// </summary>
    ViewPremoderationPending,

    /// <summary>
    /// Move blog to active (start / reopen)
    /// </summary>
    SetStatusActive,

    /// <summary>
    /// Close the blog (close / freeze / finish)
    /// </summary>
    SetStatusClosed,

    /// <summary>
    /// Edit blog settings (owner or assistant)
    /// </summary>
    EditSettings
}
