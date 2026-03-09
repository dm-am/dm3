namespace DM.Domain.Core.Identity;

/// <summary>
/// Possible error during the authentication process
/// </summary>
public enum AuthenticationError
{
    /// <summary>
    /// No error, user is authenticated
    /// </summary>
    NoError = 0,

    /// <summary>
    /// Could not find the user with given email
    /// </summary>
    WrongLogin = 1,

    /// <summary>
    /// Email was found, but the password didn't match
    /// </summary>
    WrongPassword = 2,

    /// <summary>
    /// Credentials are correct, but the user is fully banned
    /// </summary>
    Banned = 3,

    /// <summary>
    /// Email exists in PendingRegistrations (user needs to confirm email and complete registration)
    /// </summary>
    PendingRegistration = 4,

    /// <summary>
    /// Credentials are correct, but the user was removed from DB
    /// </summary>
    Removed = 5,

    /// <summary>
    /// Given authentication token has expired
    /// </summary>
    SessionExpired = 6,

    /// <summary>
    /// Unknown authentication error
    /// </summary>
    Forbidden = 7,

    /// <summary>
    /// Token is supposedly forged
    /// </summary>
    ForgedToken = 8,

    /// <summary>
    /// Account is temporarily locked due to too many failed login attempts
    /// </summary>
    AccountLocked = 9,
}
