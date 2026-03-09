using System;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Input DTO for creating a user review
/// </summary>
public class CreateUserReview
{
    /// <summary>
    /// Target user ID to review
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Review text content
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
