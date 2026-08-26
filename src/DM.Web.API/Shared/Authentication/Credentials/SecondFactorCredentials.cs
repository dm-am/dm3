using System;

namespace DM.Web.API.Shared.Authentication.Credentials;

/// <summary>
/// The second half of a login: the challenge the password step issued, and the
/// code answering it.
/// </summary>
/// <remarks>
/// Its own kind of credential rather than a flag on the login one, so that the
/// authentication pipeline routes it the way it routes every other: one switch,
/// one place that knows which domain call a kind of credential means.
/// </remarks>
public class SecondFactorCredentials : AuthCredentials
{
    /// <summary>
    /// Challenge from the short-lived cookie
    /// </summary>
    public Guid ChallengeId { get; set; }

    /// <summary>
    /// Code from the device, or a recovery code
    /// </summary>
    public string Code { get; set; } = "";
}
