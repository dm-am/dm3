using System;
using DM.Domain.Core.Authorization;

namespace DM.Workers.Mail.Sending;

/// <summary>
/// The answer this host gives to "who is the authorization about": nobody.
/// </summary>
/// <remarks>
/// Work here comes off a queue, not out of a request, so there is no current
/// user to authorize. The dependency exists because the core module registers
/// the intention manager for every host alike, and the container refuses to
/// start over an unconstructible registration since the composition validates
/// on build.
///
/// Throws rather than answering Guest, for the same reason the notification
/// dispatcher's NoIdentityProvider does: an anonymous subject is a real answer
/// that authorization would go on to decide with, and a path that reaches this
/// is a path that has to be looked at rather than served.
/// </remarks>
internal sealed class NoAuthorizationContextProvider : IAuthorizationContextProvider
{
    /// <inheritdoc />
    public IAuthorizationSubject CurrentSubject => throw new InvalidOperationException(
        "This host consumes messages and serves no request, so there is no current user to " +
        "authorize. Something on this path asked for one, which is a defect in the path " +
        "rather than a state to fall back from.");
}
