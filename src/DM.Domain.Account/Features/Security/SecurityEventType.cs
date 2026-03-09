namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Security event types for audit logging
/// </summary>
public enum SecurityEventType
{
    /// <summary>
    /// Successful login
    /// </summary>
    LoginSuccess = 1,

    /// <summary>
    /// Failed login attempt
    /// </summary>
    LoginFailure = 2,

    /// <summary>
    /// User logged out
    /// </summary>
    Logout = 3,

    /// <summary>
    /// Password was changed
    /// </summary>
    PasswordChange = 4,

    /// <summary>
    /// Email was changed
    /// </summary>
    EmailChange = 5,

    /// <summary>
    /// Session terminated by user
    /// </summary>
    SessionTerminated = 6,

    /// <summary>
    /// All other sessions terminated
    /// </summary>
    LogoutElsewhere = 7,

    /// <summary>
    /// Password reset requested
    /// </summary>
    PasswordResetRequest = 8,

    /// <summary>
    /// Password reset completed
    /// </summary>
    PasswordResetComplete = 9,

    /// <summary>
    /// Account locked due to failed attempts
    /// </summary>
    AccountLocked = 10,

    /// <summary>
    /// Suspicious login detected (new device/IP)
    /// </summary>
    SuspiciousLogin = 11
}
