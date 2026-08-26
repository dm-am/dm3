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
    SuspiciousLogin = 11,

    /// <summary>
    /// Second factor confirmed and switched on
    /// </summary>
    TwoFactorEnabled = 12,

    /// <summary>
    /// Second factor switched off by its owner
    /// </summary>
    TwoFactorDisabled = 13,

    /// <summary>
    /// A recovery code was spent
    /// </summary>
    TwoFactorRecoveryCodeUsed = 14,

    /// <summary>
    /// The set of recovery codes was reissued
    /// </summary>
    TwoFactorRecoveryCodesReissued = 15,

    /// <summary>
    /// Removal of the second factor was scheduled from the mailbox
    /// </summary>
    TwoFactorRemovalScheduled = 16,

    /// <summary>
    /// A scheduled removal of the second factor was called off
    /// </summary>
    TwoFactorRemovalCancelled = 17,

    /// <summary>
    /// The second factor was taken off by an administrator
    /// </summary>
    TwoFactorRemovedByAdmin = 18,

    /// <summary>
    /// A mailed request to take the second factor off was refused because the
    /// rank owes a factor
    /// </summary>
    /// <remarks>
    /// Its own type rather than the scheduled one it used to borrow. The entry
    /// is read by the owner of the account as a sentence, and "removal
    /// scheduled" written where nothing was scheduled sends him looking for a
    /// waiting period that does not exist.
    /// </remarks>
    TwoFactorRemovalRefused = 19,

    /// <summary>
    /// A code offered while setting the second factor up did not match
    /// </summary>
    /// <remarks>
    /// Not a failed login: there is a session, the password was not asked for
    /// and nothing was being signed into. It also stays out of the lockout
    /// counter for the same reason - a person mistyping their first setup code
    /// must not be able to lock their own way in.
    /// </remarks>
    TwoFactorSetupFailure = 20
}
