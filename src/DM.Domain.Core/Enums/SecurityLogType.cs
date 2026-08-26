namespace DM.Domain.Core.Enums;

/// <summary>
/// Which part of the security journal a caller is asking for.
/// </summary>
/// <remarks>
/// Named so the binder can refuse anything else. Spelled as a free string, the
/// filter fell past all three arms on an unrecognised word and answered with the
/// whole journal — read by the caller as the filtered slice they asked for. The
/// client was sending one of those words: "logins" for what the server calls
/// "login".
/// </remarks>
public enum SecurityLogType
{
    /// <summary>Login attempts, successful and failed.</summary>
    Login = 0,

    /// <summary>Password changes and reset requests.</summary>
    Password = 1,

    /// <summary>Session creation and termination.</summary>
    Session = 2,

    /// <summary>Everything that happened to the second factor.</summary>
    TwoFactor = 3
}
