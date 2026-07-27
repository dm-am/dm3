using System;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly IUserConnectionService _connectionService;

    /// <inheritdoc />
    public NotificationHub(
        IUserConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var (hasToken, token) = TryExtractAuthToken();
        if (hasToken)
        {
            // Registration is gated on authenticated identity inside the
            // connection service — a forged or expired token leaves the
            // connection in the same receive-only state as a guest
            await _connectionService.Add(token, Context.ConnectionId);
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

    private (bool success, string token) TryExtractAuthToken()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            return (false, string.Empty);
        }

        // BFF cookie-based auth: the SignalR negotiate/upgrade request
        // carries the same HttpOnly session cookie as regular API calls
        if (httpContext.Request.Cookies.TryGetValue(ApiCredentialsStorage.AuthCookieName, out var cookieToken) &&
            !string.IsNullOrEmpty(cookieToken))
        {
            return (true, cookieToken);
        }

        // Fallback: explicit access_token query parameter (non-browser clients)
        if (httpContext.Request.Query.TryGetValue("access_token", out var queryValues) &&
            queryValues.Any() && !string.IsNullOrEmpty(queryValues.First()))
        {
            return (true, queryValues.First()!);
        }

        return (false, string.Empty);
    }
}