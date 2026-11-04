using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Web.API.Realtime;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DM.Web.API.Notifications;

/// <summary>
/// Realtime notifications SignalR hub.
/// </summary>
/// <remarks>
/// Anonymous connections are allowed, for the public global chat broadcast. The
/// hub exposes no client-invokable methods and no groups, and per-user events go
/// through <see cref="IUserConnectionService"/>, which holds authenticated
/// connections only.
/// </remarks>
[AllowAnonymous]
public class NotificationHub : Hub<INotificationHub>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserConnectionService _connectionService;
    private readonly ICredentialsStorage _credentialsStorage;

    public NotificationHub(
        IAuthenticationService authenticationService,
        IUserConnectionService connectionService,
        ICredentialsStorage credentialsStorage)
    {
        _authenticationService = authenticationService;
        _connectionService = connectionService;
        _credentialsStorage = credentialsStorage;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The session cookie only: a token in the query string would land in the
    /// proxy's access log.
    /// </remarks>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var credentials = httpContext == null ? null : _credentialsStorage.ExtractToken(httpContext);
        if (credentials != null)
        {
            // Here, in the hub's own scope: the connection map is a singleton.
            var identity = await _authenticationService.Authenticate(credentials.Token);

            // A forged or expired token leaves the connection receive-only, as a guest.
            _connectionService.Add(identity, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionService.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
