using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;

namespace DM.Web.API.Realtime;

internal class UserConnectionService(IAuthenticationService authenticationService) : IUserConnectionService
{
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> Connections = new();

    // Reverse map so disconnect cleanup does not depend on the auth token
    // still being valid (it may already be invalidated by logout)
    private static readonly ConcurrentDictionary<string, Guid> ConnectionOwners = new();

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
        ConnectionOwners[connectionId] = userId;
        var connectionIds = Connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connectionIds)
        {
            connectionIds.Add(connectionId);
        }
    }

    public Task Remove(string connectionId)
    {
        if (!ConnectionOwners.TryRemove(connectionId, out var userId) ||
            !Connections.TryGetValue(userId, out var connectionIds))
        {
            return Task.CompletedTask;
        }

        lock (connectionIds)
        {
            connectionIds.Remove(connectionId);
            if (connectionIds.Count == 0)
            {
                Connections.TryRemove(userId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers() =>
        Connections.ToDictionary(
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