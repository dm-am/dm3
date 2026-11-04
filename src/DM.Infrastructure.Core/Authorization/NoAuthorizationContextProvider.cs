using System;
using DM.Domain.Core.Authorization;

namespace DM.Infrastructure.Core.Authorization;

/// <summary>
/// The answer a worker host gives to "who is the authorization about": nobody.
/// </summary>
/// <remarks>
/// Throws rather than answering Guest: an anonymous subject is a real answer authorization would decide with.
/// </remarks>
public sealed class NoAuthorizationContextProvider : IAuthorizationContextProvider
{
    /// <inheritdoc />
    public IAuthorizationSubject CurrentSubject => throw new InvalidOperationException(
        "This host consumes messages and serves no request, so there is no current user to " +
        "authorize. Something on this path asked for one, which is a defect in the path " +
        "rather than a state to fall back from.");
}
