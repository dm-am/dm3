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
}
