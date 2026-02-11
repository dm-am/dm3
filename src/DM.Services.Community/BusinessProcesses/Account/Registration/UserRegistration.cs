namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// DTO for new user registration (email-first flow).
/// Login is chosen later during activation.
/// </summary>
public class UserRegistration
{
    /// <summary>
    /// Email address for verification
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = null!;

    /// <summary>
    /// Indicates whether user has accepted site rules
    /// </summary>
    public bool AcceptedRules { get; set; }
}