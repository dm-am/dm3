using System;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// DTO for updating a user review entity
/// </summary>
/// <param name="ReviewId">Review identifier</param>
/// <param name="Text">Updated text (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the review</param>
public record UpdateUserReviewEntity(
    Guid ReviewId,
    string? Text = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);
