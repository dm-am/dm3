using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Web.API.Realtime;
using DM.Web.API.Shared.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DM.Web.API.Notifications;

/// <summary>
/// Realtime notifications SignalR hub.
/// </summary>
/// <remarks>
/// Anonymous connections are explicitly allowed: guests need realtime
/// global chat updates, which are public broadcasts. Safety model:
/// <list type="bullet">
/// <item>The hub exposes no client-invokable methods and uses no SignalR
/// groups — clients cannot join anything, they can only receive.</item>
/// <item>Per-user events are targeted through <see cref="IUserConnectionService"/>,
/// which registers a connection only after successful token authentication —
/// an anonymous connection can never be a per-user target.</item>
/// <item>The only event sent outside that map is the public global chat
/// broadcast (see RealtimeNotificationProcessor).</item>
/// </list>
/// </remarks>
[AllowAnonymous]
public class NotificationHub : Hub<INotificationHub>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserConnectionService _connectionService;

    /// <inheritdoc />
    public NotificationHub(
        IAuthenticationService authenticationService,
        IUserConnectionService connectionService)
    {
        _authenticationService = authenticationService;
        _connectionService = connectionService;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var (hasToken, token) = TryExtractAuthToken();
        if (hasToken)
        {
            // Authentication belongs here rather than in the connection service:
            // a hub is created per invocation from its own scope, while the
            // connection map is a process-wide singleton, and a database-backed
            // service injected into a singleton is resolved once in the root
            // scope and then shared by every connection at once.
            var identity = await _authenticationService.Authenticate(token);

            // Registration is still gated on authenticated identity inside the
            // connection service — a forged or expired token leaves the
            // connection in the same receive-only state as a guest
            _connectionService.Add(identity, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Removal is keyed by connection id, not by re-authenticating the
        // token: by disconnect time the session cookie may already be gone
        // (logout), and the mapping must not leak in that case
        await _connectionService.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <remarks>
    /// Cookie only. There used to be an access_token query-string fallback "for
    /// non-browser clients" — it had none: the SPA connects with credentials and
    /// no accessTokenFactory, and nothing else in the repository talks to the hub.
    /// What it did have was a full session credential in a URL, which the reverse
    /// proxy writes verbatim into its access log, undoing the HttpOnly guarantee
    /// the BFF design exists for. A future non-browser client belongs on the
    /// Authorization header at negotiate, not in the query string.
    /// </remarks>
    private (bool success, string token) TryExtractAuthToken()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            return (false, string.Empty);
        }

        // BFF cookie-based auth: the SignalR negotiate/upgrade request
        // carries the same HttpOnly session cookie as regular API calls
        return httpContext.Request.Cookies.TryGetValue(ApiCredentialsStorage.AuthCookieName, out var cookieToken)
               && !string.IsNullOrEmpty(cookieToken)
            ? (true, cookieToken)
            : (false, string.Empty);
    }
}
