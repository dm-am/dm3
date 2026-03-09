using System;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Input DTO for updating a user review
/// </summary>
public class UpdateUserReview
{
    /// <summary>
    /// Review ID to update
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// New review text content
    /// </summary>
    public string? Text { get; set; }
}
