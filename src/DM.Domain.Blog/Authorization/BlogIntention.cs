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
    /// Edit blog settings
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
    /// Manage blacklist
    /// </summary>
    ManageBlacklist,

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
    CreateComment
}
