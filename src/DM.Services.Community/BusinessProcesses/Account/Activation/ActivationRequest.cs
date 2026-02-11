using System;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <summary>
/// DTO for completing activation with login selection
/// </summary>
public class ActivationRequest
{
    /// <summary>
    /// Activation token from email link
    /// </summary>
    public Guid Token { get; set; }

    /// <summary>
    /// Chosen login (displayed username)
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// Optional: email from sessionStorage for idempotent retry detection.
    /// If activation already completed for this email+login, return success.
    /// </summary>
    public string? ExpectedEmail { get; set; }
}
