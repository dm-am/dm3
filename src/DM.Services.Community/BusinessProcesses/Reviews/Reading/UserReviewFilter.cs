using System;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <summary>
/// Filter parameters for user reviews
/// </summary>
public class UserReviewFilter
{
    /// <summary>
    /// Filter by review author ID
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by recipient (reviewed user) ID
    /// </summary>
    public Guid? RecipientId { get; set; }
}
