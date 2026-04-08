using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Input DTO for updating a user endorsement
/// </summary>
public class UpdateUserEndorsement
{
    /// <summary>
    /// Endorsement ID to update
    /// </summary>
    public Guid EndorsementId { get; set; }

    /// <summary>
    /// New endorsement text content (positive only)
    /// </summary>
    public string? Text { get; set; }
}
