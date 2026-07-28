using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;

namespace DM.Web.API.Realtime;

internal class UserConnectionService(IAuthenticationService authenticationService) : IUserConnectionService
{
    // Instance state, not static: the process-wide lifetime comes from the
    // single-instance registration. A static map would additionally survive
    // across container instances, which is what makes it leak between tests.
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    // Reverse map so disconnect cleanup does not depend on the auth token
    // still being valid (it may already be invalidated by logout)
    private readonly ConcurrentDictionary<string, Guid> _connectionOwners = new();

    public async Task Add(string authToken, string connectionId)
    {
        var identity = await authenticationService.Authenticate(authToken);
        if (!identity.User.IsAuthenticated)
        {
            // Guests keep an open connection for public broadcasts, but are
            // never registered here — per-user targeting only ever resolves
            // connections of authenticated users
            return;
        }

        var userId = identity.User.UserId;
        _connectionOwners[connectionId] = userId;
        var connectionIds = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connectionIds)
        {
            connectionIds.Add(connectionId);
        }
    }

    public Task Remove(string connectionId)
    {
        if (!_connectionOwners.TryRemove(connectionId, out var userId) ||
            !_connections.TryGetValue(userId, out var connectionIds))
        {
            return Task.CompletedTask;
        }

        lock (connectionIds)
        {
            connectionIds.Remove(connectionId);
            if (connectionIds.Count == 0)
            {
                _connections.TryRemove(userId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers() =>
        _connections.ToDictionary(
            k => k.Key,
            v =>
            {
                // Snapshot under the same lock used for mutations — the
                // previous lazy enumeration could observe a set being
                // modified by a concurrent connect/disconnect
                lock (v.Value)
                {
                    return (IEnumerable<string>)v.Value.ToArray();
                }
            });
}