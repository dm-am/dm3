using System;
using System.Collections.Generic;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Realtime;

/// <summary>
/// SignalR connected users service
/// </summary>
/// <remarks>
/// Tracks authenticated connections only, so a guest connection can never be
/// targeted by per-user events.
/// </remarks>
public interface IUserConnectionService
{
    /// <summary>
    /// Save the connection of an already authenticated identity.
    /// No-op when the identity is not authenticated.
    /// </summary>
    void Add(IIdentity identity, string connectionId);

    /// <summary>
    /// Remove a connection by its id, so cleanup works even when the session was
    /// invalidated before the socket closed.
    /// </summary>
    void Remove(string connectionId);

    /// <summary>
    /// Get all connected users
    /// </summary>
    /// <returns>Dictionary mapping user IDs to their connection IDs</returns>
    IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers();
}
