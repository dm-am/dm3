namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// A requested premoderation transition for a blog curated by a mentor.
/// The endpoint is gated Mentor+; the service rejects a transition applied
/// from an incompatible current premoderation state with BadRequest.
/// </summary>
public enum BlogPremoderationTransition
{
    /// <summary>
    /// Submit for premoderation: AwaitingEdits -> AwaitingApproval
    /// (assigns the acting mentor as curator)
    /// </summary>
    SendToPremoderation = 0,

    /// <summary>
    /// Release from premoderation: AwaitingApproval -> Approved
    /// (clears the curator, blog becomes publicly visible)
    /// </summary>
    RemoveFromPremoderation = 1
}
