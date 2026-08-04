using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Realtime;

// Deliberately without constructor dependencies: the registration is a single
// instance, so anything taken here is activated once in the root scope and kept
// for the life of the process. Authentication is database-backed, and the pooled
// DbContext at the end of that chain would then be shared by every connection at
// once: two clients connecting at the same moment are two operations on one
// context, and its change tracker would hold every user that ever connected.
// The caller authenticates in its own per-invocation scope and hands the result
// over already resolved.
internal class UserConnectionService : IUserConnectionService
{
    // Instance state, not static: the process-wide lifetime comes from the
    // single-instance registration. A static map would additionally survive
    // across container instances, which is what makes it leak between tests.
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    // Reverse map so disconnect cleanup does not depend on the auth token
    // still being valid (it may already be invalidated by logout)
    private readonly ConcurrentDictionary<string, Guid> _connectionOwners = new();

    public void Add(IIdentity identity, string connectionId)
    {
        if (!identity.User.IsAuthenticated)
        {
            // Guests keep an open connection for public broadcasts, but are
            // never registered here — per-user targeting only ever resolves
            // connections of authenticated users
            return;
        }

        var userId = identity.User.UserId;
        _connectionOwners[connectionId] = userId;

        // Retried until the set the connection lands in is still the one the map
        // holds. GetOrAdd hands back a set that a disconnect of the last
        // connection of the same user drops from the map a moment later, and the
        // connection then sits in an object nothing can reach: present in the
        // owners map, absent from GetConnectedUsers, and skipped by every per-user
        // push until the client reconnects. Removal happens under this same lock,
        // so a set that is still the map's while the lock is held cannot be
        // dropped, and the addition makes it non-empty for good.
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
                // Only while the map still holds this very set. An unconditional
                // removal by key drops whatever took its place: a second
                // disconnect that read the set before it was emptied would
                // otherwise take out the set a fresh connection had just been
                // registered in.
                ((ICollection<KeyValuePair<Guid, HashSet<string>>>)_connections)
                    .Remove(new KeyValuePair<Guid, HashSet<string>>(userId, connectionIds));
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
