namespace DM.Services.Community.BusinessProcesses.Account.Recovery;

/// <summary>
/// Result of recovery request
/// </summary>
public enum RecoveryResult
{
    /// <summary>
    /// Password reset email sent (active user)
    /// </summary>
    PasswordReset,

    /// <summary>
    /// Activation email resent (pending registration)
    /// </summary>
    ActivationResent,

    /// <summary>
    /// Email not found in database
    /// </summary>
    NotFound
}
