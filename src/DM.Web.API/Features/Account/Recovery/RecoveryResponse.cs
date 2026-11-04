namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// Response for account recovery request
/// </summary>
public class RecoveryResponse
{
    /// <summary>
    /// Result status of the recovery request
    /// </summary>
    public RecoveryStatus Status { get; set; }

    /// <summary>
    /// Email address that was checked
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// How long the link in the letter lives, in hours; absent when no letter went out
    /// </summary>
    /// <example>48</example>
    public int? LinkLifetimeHours { get; set; }
}

/// <summary>
/// Status of recovery request
/// </summary>
public enum RecoveryStatus
{
    /// <summary>
    /// Password reset email was sent
    /// </summary>
    PasswordResetSent,

    /// <summary>
    /// Activation email was resent
    /// </summary>
    ActivationResent,

    /// <summary>
    /// Email not found in system
    /// </summary>
    NotFound
}
