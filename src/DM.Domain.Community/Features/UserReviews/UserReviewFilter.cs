using System;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Filter parameters for user reviews
/// </summary>
public class UserReviewFilter
{
    /// <summary>
    /// Filter by review author
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Filter by review recipient (target user)
    /// </summary>
    public Guid? RecipientId { get; set; }
}
