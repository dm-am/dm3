namespace DM.Domain.Blog.Authorization;

public enum BlogIntention
{
    Create,

    /// <summary>
    /// Owner-level blog editing (assistant removal, blacklist management)
    /// </summary>
    Edit,

    Delete,
    ManageRubrics,
    CreatePublication,
    ViewDraft,
    InviteAssistant,
    InviteReader,
    CancelInvitation,
    AssignMentor,
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

    ViewPremoderationPending,

    /// <summary>
    /// Move blog to active (start / reopen)
    /// </summary>
    SetStatusActive,

    /// <summary>
    /// Close the blog (close / freeze / finish)
    /// </summary>
    SetStatusClosed,

    EditSettings
}
