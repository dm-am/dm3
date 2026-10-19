using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Preferences;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Response for successful login or activation
/// </summary>
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
    /// A success with a stage on it, not a refusal: the credentials are right.
    /// The challenge travels in a cookie, never in this body.
    /// </remarks>
    public bool TwoFactorRequired { get; set; }
}
