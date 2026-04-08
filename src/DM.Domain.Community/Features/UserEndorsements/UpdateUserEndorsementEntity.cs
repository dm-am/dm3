using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// DTO for updating a user endorsement entity
/// </summary>
/// <param name="EndorsementId">Endorsement identifier</param>
/// <param name="Text">Updated text (null to keep current)</param>
/// <param name="IsRemoved">Updated removed status (null to keep current)</param>
/// <param name="ModifiedUtc">Modification timestamp</param>
/// <param name="ModifiedByUserId">User who modified the endorsement</param>
public record UpdateUserEndorsementEntity(
    Guid EndorsementId,
    string? Text = null,
    bool? IsRemoved = null,
    DateTimeOffset? ModifiedUtc = null,
    Guid? ModifiedByUserId = null);
