namespace DM.Domain.Account.Configuration;

/// <summary>
/// Configuration for token lifetimes
/// </summary>
public class TokenConfiguration
{
    /// <summary>
    /// Password reset token lifetime in hours (default: 24)
    /// </summary>
    public int PasswordResetTokenLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Account activation token lifetime in hours (default: 48)
    /// </summary>
    public int ActivationTokenLifetimeHours { get; set; } = 48;

    /// <summary>
    /// Email change confirmation token lifetime in hours (default: 24)
    /// </summary>
    public int EmailChangeTokenLifetimeHours { get; set; } = 24;

    /// <summary>
    /// Lifetime in hours of the link that asks for a mailed removal of the
    /// second factor (default: 24)
    /// </summary>
    /// <remarks>
    /// Only the asking half has a lifetime of its own. The link that calls the
    /// removal off is live for exactly as long as there is a removal to call
    /// off: a cancellation that expired before the thing it cancels would be a
    /// promise made in the letter and broken by the clock.
    /// </remarks>
    public int TwoFactorRemovalTokenLifetimeHours { get; set; } = 24;
}
