using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Preferences;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Response for successful login or activation
/// </summary>
/// <remarks>
/// Returned by:
/// - POST /v1/account/login
/// - POST /v1/account/activation
/// - POST /v1/account/password-reset
///
/// Contains everything needed to start a session - theme, UI preferences.
/// </remarks>
public class LoginResponse
{
    /// <summary>
    /// Authenticated user information
    /// </summary>
    /// <remarks>
    /// Absent while <see cref="TwoFactorRequired" /> is true: the login is not
    /// finished, so there is no session and nothing to describe.
    /// </remarks>
    public User? User { get; set; }

    /// <summary>
    /// User preferences for UI customization
    /// </summary>
    /// <remarks>
    /// Absent for the same reason as <see cref="User" />.
    /// </remarks>
    public Preferences? Preferences { get; set; }

    /// <summary>
    /// The password was accepted and a second factor is still owed
    /// </summary>
    /// <remarks>
    /// A successful answer with a stage on it rather than a refusal, and that is
    /// a decision: the credentials are right and the flow continues. The contract
    /// carries no machine-readable error code - the type of an error is its
    /// status - so "now enter the code" cannot be an error without inventing one.
    ///
    /// The challenge itself travels in a short-lived cookie and never in this
    /// body: in the body a single-page client has to hold it in memory, where it
    /// is lost by a refresh halfway through a login and tempting to put into
    /// local storage.
    /// </remarks>
    public bool TwoFactorRequired { get; set; }
}
