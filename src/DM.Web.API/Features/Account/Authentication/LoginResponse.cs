using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Preferences;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// Response for successful login or activation
/// </summary>
/// <remarks>
/// Returned by:
/// - POST /v1/account/login
/// - POST /v1/account/activation/{token}
/// - POST /v1/account/password-reset/{token}
///
/// Contains everything needed to start a session - theme, UI preferences.
/// </remarks>
public class LoginResponse
{
    /// <summary>
    /// Authenticated user information
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// User preferences for UI customization
    /// </summary>
    public Preferences Preferences { get; set; } = null!;
}
