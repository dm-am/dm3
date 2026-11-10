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
    public AuthenticatedUser User { get; private init; } = AuthenticatedUser.Guest;

    /// <inheritdoc />
    public Session? Session { get; private init; }

    /// <inheritdoc />
    public UserSettings Settings { get; private init; } = UserSettings.Default;

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
    /// <param name="lockoutSecondsRemaining">
    /// Seconds left on the lockout, for the one error that has them.
    /// </param>
    public static IIdentity Fail(
        AuthenticationError error, int? lockoutSecondsRemaining = null) => new Identity
        {
            Error = error,
            LockoutSecondsRemaining = lockoutSecondsRemaining
        };

    /// <summary>
    /// Creates the identity of a sign-in refused by a full ban
    /// </summary>
    /// <param name="endsUtc">When the ban runs out, or null for one with no end</param>
    /// <returns>Unauthenticated user identity carrying the ban's end</returns>
    public static IIdentity Banned(DateTimeOffset? endsUtc) => new Identity
    {
        Error = AuthenticationError.Banned,
        BanEndsUtc = endsUtc
    };

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
    /// The guest user and no token: every check asks whether the user is
    /// authenticated, and until the second factor the answer is no.
    /// </remarks>
    /// <param name="challengeId">Identifier of the issued challenge</param>
    /// <returns>Identity of a login that is not finished</returns>
    public static IIdentity SecondFactorRequired(Guid challengeId) => new Identity
    {
        Error = AuthenticationError.NoError,
        TwoFactorChallengeId = challengeId
    };

    /// <summary>
    /// Creates identity for the guest user
    /// </summary>
    /// <returns>Guest user identity</returns>
    public static IIdentity Guest() => new Identity
    {
        Error = AuthenticationError.NoError
    };
}
