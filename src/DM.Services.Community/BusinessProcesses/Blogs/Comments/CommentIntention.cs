namespace DM.Services.Community.BusinessProcesses.Blogs.Comments;

/// <summary>
/// List of blog comment actions that requires authorization
/// </summary>
public enum CommentIntention
{
    /// <summary>
    /// Edit comment
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Remove comment
    /// </summary>
    Delete = 2,

    /// <summary>
    /// Like comment
    /// </summary>
    Like = 3
}
