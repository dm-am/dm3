using System;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// DTO for updating a platform review entity
/// </summary>
/// <param name="ReviewId">Review identifier</param>
/// <param name="Text">Updated text (null to keep current)</param>
/// <param name="IsApproved">Updated approval status (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the review</param>
public record UpdatePlatformReviewEntity(
    Guid ReviewId,
    string? Text = null,
    bool? IsApproved = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);
