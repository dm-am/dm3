namespace DM.Domain.Account.Authorization;

/// <summary>
/// Intentions for account management operations
/// </summary>
public enum AccountIntention
{
    /// <summary>
    /// Change own password (authenticated user)
    /// </summary>
    ChangePassword,

    /// <summary>
    /// Change own email (authenticated user)
    /// </summary>
    ChangeEmail,

    /// <summary>
    /// Deactivate own account (authenticated user)
    /// </summary>
    Deactivate,

    /// <summary>
    /// View own active sessions (authenticated user)
    /// </summary>
    ViewSessions,

    /// <summary>
    /// Terminate own session (authenticated user)
    /// </summary>
    TerminateSession,

    /// <summary>
    /// Take back a completed username change of another user (senior moderator)
    /// </summary>
    RollbackUsernameChange
}
