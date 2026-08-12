using System.Collections.Generic;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Which event types make up each of the journal's filters.
/// </summary>
/// <remarks>
/// The answer to "what counts as a login event" is a statement about the product,
/// not about storage, and it used to live inside the Mongo repository — three
/// arrays built beside three otherwise identical queries. A type added to
/// <see cref="SecurityEventType" /> had to be remembered there, in a file nobody
/// opens to think about the journal, and the omission showed up as an entry the
/// reader simply never saw under the filter it belongs to.
///
/// Declared next to the enum for that reason: adding a member and deciding which
/// filter it answers to are one act, and they are now one place.
/// </remarks>
public static class SecurityEventCategories
{
    /// <summary>Attempts to sign in, successful and not.</summary>
    public static readonly IReadOnlyList<SecurityEventType> Login =
    [
        SecurityEventType.LoginSuccess,
        SecurityEventType.LoginFailure,
        SecurityEventType.SuspiciousLogin
    ];

    /// <summary>Sign-ins that succeeded, which is what "your last logins" means.</summary>
    /// <remarks>
    /// Separate from <see cref="Login" /> rather than filtered out of it: the
    /// limit applies before the filter, so a run of wrong passwords would push
    /// every earlier success out of the window and the list would come back empty.
    /// </remarks>
    public static readonly IReadOnlyList<SecurityEventType> SuccessfulLogins =
    [
        SecurityEventType.LoginSuccess
    ];

    /// <summary>Everything that changed the password or asked to.</summary>
    public static readonly IReadOnlyList<SecurityEventType> Password =
    [
        SecurityEventType.PasswordChange,
        SecurityEventType.PasswordResetRequest,
        SecurityEventType.PasswordResetComplete
    ];

    /// <summary>Sessions ending, whether the reader ended them or somebody else did.</summary>
    public static readonly IReadOnlyList<SecurityEventType> Session =
    [
        SecurityEventType.Logout,
        SecurityEventType.SessionTerminated,
        SecurityEventType.LogoutElsewhere
    ];
}
