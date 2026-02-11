using System;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <summary>
/// Filter parameters for post reviews queries
/// </summary>
public class PostReviewFilter
{
    /// <summary>
    /// Filter by author ID (reviews BY this user)
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by post author ID (reviews ON this user's posts)
    /// </summary>
    public Guid? RecipientId { get; set; }

    /// <summary>
    /// Filter by game ID (reviews on posts in this game)
    /// </summary>
    public Guid? GameId { get; set; }
}
