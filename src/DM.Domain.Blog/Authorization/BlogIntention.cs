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
    /// Deliver a premoderation verdict on somebody else's blog: approve it, or
    /// send it back for edits. Site-wide and targetless — the rule is a rank
    /// (Mentor and everybody above), not a relationship with the blog.
    /// </summary>
    SetStatusModeration,

    /// <summary>
    /// Ask for a premoderation verdict on your own blog. The owner's move and
    /// nobody else's: not an assistant's, not the mentor's. Kept apart from
    /// <see cref="EditSettings" /> on purpose — the people who may fill the blog's
    /// form in are not the person who may declare it ready.
    /// </summary>
    SubmitForApproval,

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
