using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// DTO for creating a user endorsement entity
/// </summary>
public class CreateUserEndorsementEntity
{
    /// <summary>
    /// Endorsement identifier
    /// </summary>
    public Guid EndorsementId { get; set; }

    /// <summary>
    /// Author user identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Target user identifier (the user being endorsed)
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Endorsement text (positive only)
    /// </summary>
    public required string Text { get; set; }
}
