namespace DM.Domain.Core.Identity;

/// <summary>
/// Authentication result provider
/// </summary>
public interface IIdentity
{
    /// <summary>
    /// Current authenticated user
    /// </summary>
    AuthenticatedUser User { get; }

    /// <summary>
    /// Current authentication session
    /// </summary>
    Session? Session { get; }

    /// <summary>
    /// Current authenticated user settings
    /// </summary>
    UserSettings Settings { get; }

    /// <summary>
    /// Current authentication error state
    /// </summary>
    AuthenticationError Error { get; }

    /// <summary>
    /// Current authentication token, provided or generated
    /// </summary>
    string? AuthenticationToken { get; }

    /// <summary>
    /// The challenge issued because the password was proven and a second factor
    /// is still owed.
    /// </summary>
    /// <remarks>
    /// Not a session and never mistakable for one: while this is set,
    /// <see cref="User" /> is the guest, <see cref="Session" /> is null and
    /// <see cref="AuthenticationToken" /> is null, so no existing check can be
    /// tricked into treating the state as signed in. Nothing else about
    /// authorization reads it - it exists for the one endpoint that finishes the
    /// login, and for the cookie that carries the challenge to it.
    /// </remarks>
    Guid? TwoFactorChallengeId { get; }
}
