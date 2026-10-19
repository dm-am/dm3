using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Realtime;

// No constructor dependencies: a singleton keeps what it takes for the life of the
// process, and the pooled DbContext behind authentication would be shared by every
// connection at once. The caller authenticates in its own scope.
internal class UserConnectionService : IUserConnectionService
{
    // Instance state, not static: a static map leaks between container instances.
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    // Reverse map, so a disconnect does not need the token, which logout may have invalidated.
    private readonly ConcurrentDictionary<string, Guid> _connectionOwners = new();

    public void Add(IIdentity identity, string connectionId)
    {
        if (!identity.User.IsAuthenticated)
        {
            return;
        }

        var userId = identity.User.UserId;
        _connectionOwners[connectionId] = userId;

        // Retried until the set is still the one the map holds: a disconnect of the
        // user's last connection may drop the set GetOrAdd returned, and removal
        // happens under the same lock.
        while (true)
        {
            var connectionIds = _connections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (connectionIds)
            {
                if (_connections.TryGetValue(userId, out var current) &&
                    ReferenceEquals(current, connectionIds))
                {
                    connectionIds.Add(connectionId);
                    return;
                }
            }
        }
    }

    public void Remove(string connectionId)
    {
        if (!_connectionOwners.TryRemove(connectionId, out var userId) ||
            !_connections.TryGetValue(userId, out var connectionIds))
        {
            return;
        }

        lock (connectionIds)
        {
            connectionIds.Remove(connectionId);
            if (connectionIds.Count == 0)
            {
                // Only while the map still holds this very set: a removal by key
                // would drop a set a fresh connection had just been registered in.
                ((ICollection<KeyValuePair<Guid, HashSet<string>>>)_connections)
                    .Remove(new KeyValuePair<Guid, HashSet<string>>(userId, connectionIds));
            }
        }
    }

    public IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers() =>
        _connections.ToDictionary(
            k => k.Key,
            v =>
            {
                // A snapshot under the lock the mutations take.
                lock (v.Value)
                {
                    return (IEnumerable<string>)v.Value.ToArray();
                }
            });
}
