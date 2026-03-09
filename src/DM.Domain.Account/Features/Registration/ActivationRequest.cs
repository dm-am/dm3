using System;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// DTO for completing activation with username selection
/// </summary>
public class ActivationRequest
{
    /// <summary>
    /// Activation token from email link
    /// </summary>
    public Guid Token { get; set; }

    /// <summary>
    /// Chosen username (unique display name)
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Optional: email from sessionStorage for idempotent retry detection.
    /// If activation already completed for this email+username, returns success instead of error.
    /// </summary>
    public string? RetryEmail { get; set; }
}
