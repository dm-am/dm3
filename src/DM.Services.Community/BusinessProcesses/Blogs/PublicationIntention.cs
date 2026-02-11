namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <summary>
/// Publication-related intentions
/// </summary>
public enum PublicationIntention
{
    /// <summary>
    /// Edit publication
    /// </summary>
    Edit,

    /// <summary>
    /// Delete publication
    /// </summary>
    Delete,

    /// <summary>
    /// Publish (make visible)
    /// </summary>
    Publish,

    /// <summary>
    /// View unpublished (draft)
    /// </summary>
    ViewDraft,

    /// <summary>
    /// Like publication
    /// </summary>
    Like,

    /// <summary>
    /// Create comment on publication
    /// </summary>
    CreateComment
}
