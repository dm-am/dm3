namespace DM.Domain.Core.Authorization;

/// <summary>
/// List of comment actions that requires authorization
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
