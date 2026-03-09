using System;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Data for updating a platform review
/// </summary>
public class UpdatePlatformReview
{
    /// <summary>
    /// Review identifier
    /// </summary>
    public required Guid ReviewId { get; init; }

    /// <summary>
    /// Updated review text
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Approval status (for moderation)
    /// </summary>
    public bool? Approved { get; init; }
}
