namespace DM.Domain.Account.Configuration;

/// <summary>
/// Configuration for token lifetimes
/// </summary>
public class TokenConfiguration
{
    /// <summary>
    /// Password reset token lifetime in hours
    /// </summary>
    /// <remarks>
    /// The same window as the activation link: both letters are read by somebody
    /// who cannot get in.
    /// </remarks>
    public int PasswordResetTokenLifetimeHours { get; set; } = 48;

    /// <summary>
    /// Account activation token lifetime in hours
    /// </summary>
    public int ActivationTokenLifetimeHours { get; set; } = 48;

    /// <summary>
    /// Email change confirmation token lifetime in hours
    /// </summary>
    public int EmailChangeTokenLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Lifetime in hours of the link that asks for a mailed removal of the
    /// second factor
    /// </summary>
    /// <remarks>
    /// The link that calls the removal off lives as long as the removal it cancels.
    /// </remarks>
    public int TwoFactorRemovalTokenLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Lifetime in hours of the link a moderator's approval of a username change mails
    /// </summary>
    public int UsernameChangeApprovalTokenLifetimeHours { get; set; } = 48;
}
