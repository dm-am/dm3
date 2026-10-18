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

    /// <summary>
    /// Seconds left on the lockout, when the answer is one.
    /// </summary>
    /// <remarks>
    /// Set only with <see cref="AuthenticationError.AccountLocked" /> and null
    /// on every other answer. The number is the whole difference between a door
    /// that is shut and a door that is shut until a stated moment: the reader
    /// acts on it, and without it the screen can only say "later".
    /// </remarks>
    int? LockoutSecondsRemaining { get; }

    /// <summary>
    /// When the ban that refused this sign-in runs out, when it has an end.
    /// </summary>
    /// <remarks>
    /// Set only with <see cref="AuthenticationError.Banned" />. Null there means
    /// a ban with no end the reader would believe; null anywhere else means
    /// nothing. The refusal that carries it was earned by a proven password,
    /// which is the one condition under which the site names what moderation
    /// did to an account (SECURITY.md).
    /// </remarks>
    DateTimeOffset? BanEndsUtc { get; }
}
