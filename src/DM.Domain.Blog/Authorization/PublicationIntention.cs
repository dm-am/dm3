namespace DM.Domain.Blog.Authorization;

/// <summary>
/// Publication-related intentions
/// </summary>
public enum PublicationIntention
{
    /// <summary>
    /// View draft publication
    /// </summary>
    ViewDraft,

    /// <summary>
    /// Edit publication
    /// </summary>
    Edit,

    /// <summary>
    /// Publish a draft publication
    /// </summary>
    Publish,

    /// <summary>
    /// Delete publication
    /// </summary>
    Delete,

    /// <summary>
    /// Create comment on publication
    /// </summary>
    CreateComment,

    /// <summary>
    /// Like publication
    /// </summary>
    Like
}
