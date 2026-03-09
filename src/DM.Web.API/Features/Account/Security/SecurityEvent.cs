using System;

namespace DM.Web.API.Features.Account.Security;

/// <summary>
/// Security event type
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

/// <summary>
/// Security event for API responses
/// </summary>
public class SecurityEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of security event
    /// </summary>
    public SecurityEventType EventType { get; set; }

    /// <summary>
    /// Human-readable event description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Device/browser info
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Additional details
    /// </summary>
    public string? Details { get; set; }
}
