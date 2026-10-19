using System;
using DM.Domain.Core.Identity;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The answer this host gives to "who is doing this": nobody.
/// </summary>
/// <remarks>
/// Throws rather than answering anonymous: an anonymous identity is a real answer code goes on to decide with.
/// </remarks>
internal sealed class NoIdentityProvider : IIdentityProvider
{
    /// <inheritdoc />
    public IIdentity Current => throw new InvalidOperationException(
        "This host consumes messages and serves no request, so there is no current user. " +
        "Something on this path asked for one, which is a defect in the path rather than a " +
        "state to fall back from.");
}
