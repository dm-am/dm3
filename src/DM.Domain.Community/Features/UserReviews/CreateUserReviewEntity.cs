using System;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// DTO for creating a user review entity
/// </summary>
public class CreateUserReviewEntity
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public Guid ReviewId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Target user identifier (the user being reviewed)
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Review text
    /// </summary>
    public required string Text { get; set; }
}
