namespace DM.Domain.Core.Enums;

/// <summary>
/// Which part of the security journal a caller is asking for.
/// </summary>
/// <remarks>
/// An enum so the binder refuses an unknown filter.
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
