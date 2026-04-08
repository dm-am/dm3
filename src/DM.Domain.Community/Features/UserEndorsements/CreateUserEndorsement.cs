using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Input DTO for creating a user endorsement
/// </summary>
public class CreateUserEndorsement
{
    /// <summary>
    /// Target user ID to endorse
    /// </summary>
    public Guid TargetUserId { get; set; }

    /// <summary>
    /// Endorsement text content (positive only)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
