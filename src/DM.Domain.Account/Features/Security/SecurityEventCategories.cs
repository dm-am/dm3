using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Which event types make up each of the journal's filters.
/// </summary>
/// <remarks>
/// Next to <see cref="SecurityEventType" />: adding a member and deciding which
/// filter it answers to are one act.
/// </remarks>
public static class SecurityEventCategories
{
    /// <summary>The types a journal filter is made of, or null for no filter.</summary>
    /// <param name="type">Filter the reader asked for</param>
    public static IReadOnlyList<SecurityEventType>? For(SecurityLogType? type) => type switch
    {
        SecurityLogType.Login => Login,
        SecurityLogType.Password => Password,
        SecurityLogType.Session => Session,
        SecurityLogType.TwoFactor => TwoFactor,
        _ => null
    };

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

    /// <summary>Everything that happened to the second factor of the account.</summary>
    public static readonly IReadOnlyList<SecurityEventType> TwoFactor =
    [
        SecurityEventType.TwoFactorEnabled,
        SecurityEventType.TwoFactorDisabled,
        SecurityEventType.TwoFactorRecoveryCodeUsed,
        SecurityEventType.TwoFactorRecoveryCodesReissued,
        SecurityEventType.TwoFactorRemovalScheduled,
        SecurityEventType.TwoFactorRemovalCancelled,
        SecurityEventType.TwoFactorRemovedByAdmin,
        SecurityEventType.TwoFactorSetupFailure
    ];
}
