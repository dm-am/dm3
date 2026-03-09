using System;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// DTO for creating a platform review entity
/// </summary>
public class CreatePlatformReviewEntity
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
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Review text
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Whether the review is approved
    /// </summary>
    public bool IsApproved { get; set; }
}
