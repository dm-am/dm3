using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The answer this host gives to "who is doing this": nobody.
/// </summary>
/// <remarks>
/// Work here comes off a queue, not out of a request, so there is no current
/// user and there cannot be one. The dependency exists because the notification
/// service declares it for its read and mark methods, and this host calls only
/// the write.
///
/// It was satisfied by registering the whole account domain — a scan of an
/// assembly, its mapper profiles and its module, pulled in for one interface,
/// which also obliged this host to bind four option sections it has no use for
/// and brought along an authorization context answering Guest. Two costs behind
/// that: a worker holding a session encryption key it never uses, and an
/// authorization question in this process answered as an anonymous visitor
/// rather than refused.
///
/// Throws rather than returning an anonymous identity, because the two are not
/// the same thing. An anonymous identity is a real answer — the reader who is not
/// logged in — and code that gets one goes on to make decisions with it. There is
/// no reader here at all, and a path that reaches this is a path that has to be
/// looked at rather than served.
/// </remarks>
internal sealed class NoIdentityProvider : IIdentityProvider, IAuthorizationContextProvider
{
    private static InvalidOperationException NoUser() => new(
        "This host consumes messages and serves no request, so there is no current user. " +
        "Something on this path asked for one, which is a defect in the path rather than a " +
        "state to fall back from.");

    /// <inheritdoc />
    public IIdentity Current => throw NoUser();

    /// <summary>
    /// The authorization face of the same refusal. The domain services this
    /// host scans in reach the intention manager, whose graph must resolve at
    /// startup; answering Guest here would be a real answer to a question that
    /// has no reader behind it.
    /// </summary>
    public IAuthorizationSubject CurrentSubject => throw NoUser();
}
