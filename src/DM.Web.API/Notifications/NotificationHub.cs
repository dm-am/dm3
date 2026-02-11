using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Web.Core.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DM.Web.API.Notifications;

/// <inheritdoc />
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
    public override Task OnConnectedAsync()
    {
        var (hasToken, token) = TryExtractAuthToken();
        if (hasToken)
        {
            _connectionService.Add(token, Context.ConnectionId);
        }

        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var (hasToken, token) = TryExtractAuthToken();
        if (hasToken)
        {
            _connectionService.Remove(token, Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    private (bool success, string token) TryExtractAuthToken()
    {
        string? token = null;
        var httpContext = Context.GetHttpContext();
        if (httpContext == null ||
            !httpContext.Request.Query.TryGetValue("access_token", out var queryValues) ||
            !queryValues.Any() || string.IsNullOrEmpty(token = queryValues.First()))
        {
            return (false, string.Empty);
        }

        return (true, token);
    }
}