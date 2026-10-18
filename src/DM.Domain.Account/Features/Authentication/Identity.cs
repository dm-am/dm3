using System;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
public class Identity : IIdentity
{
    private Identity()
    {
    }

    /// <inheritdoc />
    public AuthenticatedUser User { get; private init; } = null!;

    /// <inheritdoc />
    public Session? Session { get; private init; }

    /// <inheritdoc />
    public UserSettings Settings { get; private init; } = null!;

    /// <inheritdoc />
    public AuthenticationError Error { get; private init; }

    /// <inheritdoc />
    public string? AuthenticationToken { get; private init; }

    /// <inheritdoc />
    public Guid? TwoFactorChallengeId { get; private init; }

    /// <inheritdoc />
    public int? LockoutSecondsRemaining { get; private init; }

    /// <inheritdoc />
    public DateTimeOffset? BanEndsUtc { get; private init; }

    /// <summary>
    /// Creates identity for an unauthenticated user
    /// </summary>
    /// <param name="error">Authentication error</param>
    /// <param name="lockoutSecondsRemaining">
    /// Seconds left on the lockout, for the one error that has them.
    /// </param>
    /// <returns>Unauthenticated user identity</returns>
    public static IIdentity Fail(
        AuthenticationError error, int? lockoutSecondsRemaining = null) => new Identity
        {
            Error = error,
            LockoutSecondsRemaining = lockoutSecondsRemaining,
            User = AuthenticatedUser.Guest,
            Settings = UserSettings.Default,
            AuthenticationToken = null,
            Session = null
        };

    /// <summary>
    /// Creates the identity of a sign-in refused by a full ban
    /// </summary>
    /// <param name="endsUtc">When the ban runs out, or null for one with no end</param>
    /// <returns>Unauthenticated user identity carrying the ban's end</returns>
    public static IIdentity Banned(DateTimeOffset? endsUtc) => new Identity
    {
        Error = AuthenticationError.Banned,
        BanEndsUtc = endsUtc,
        User = AuthenticatedUser.Guest,
        Settings = UserSettings.Default,
        AuthenticationToken = null,
        Session = null
    };

    /// <summary>
    /// Creates identity for an authenticated user
    /// </summary>
    /// <param name="user">Authenticated user</param>
    /// <param name="session">User session</param>
    /// <param name="settings">User settings</param>
    /// <param name="token">Authentication token</param>
    /// <returns>Authenticated user identity</returns>
    public static IIdentity Success(
        AuthenticatedUser user, Session session, UserSettings settings, string token) => new Identity
        {
            Error = AuthenticationError.NoError,
            User = user,
            Settings = settings,
            AuthenticationToken = token,
            Session = session
        };

    /// <summary>
    /// Creates the state between the two factors: the password is proven, the
    /// challenge is issued, and no session exists.
    /// </summary>
    /// <remarks>
    /// The guest user and no token, deliberately. Every check in the product
    /// asks whether the user is authenticated, and the answer here has to be no
    /// until the second factor is shown - anything else would be a session
    /// wearing a flag that some surface eventually forgets to read.
    /// </remarks>
    /// <param name="challengeId">Identifier of the issued challenge</param>
    /// <returns>Identity of a login that is not finished</returns>
    public static IIdentity SecondFactorRequired(Guid challengeId) => new Identity
    {
        Error = AuthenticationError.NoError,
        User = AuthenticatedUser.Guest,
        Settings = UserSettings.Default,
        AuthenticationToken = null,
        Session = null,
        TwoFactorChallengeId = challengeId
    };

    /// <summary>
    /// Creates identity for the guest user
    /// </summary>
    /// <returns>Guest user identity</returns>
    public static IIdentity Guest() => new Identity
    {
        Error = AuthenticationError.NoError,
        User = AuthenticatedUser.Guest,
        Settings = UserSettings.Default,
        AuthenticationToken = null,
        Session = null
    };
}
