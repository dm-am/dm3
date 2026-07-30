using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Realtime;

/// <summary>
/// SignalR connected users service
/// </summary>
/// <remarks>
/// Tracks authenticated connections only: registration requires an authenticated
/// identity, so anonymous (guest) connections never appear here and can never be
/// targeted by per-user events.
/// </remarks>
public interface IUserConnectionService
{
    /// <summary>
    /// Save the connection of an already authenticated identity.
    /// No-op when the identity is not authenticated.
    /// </summary>
    /// <remarks>
    /// The identity is resolved by the caller and not here on purpose: this service
    /// is a process-wide singleton, and authentication is database-backed.
    /// </remarks>
    /// <param name="identity">Identity resolved by the caller in its own scope</param>
    /// <param name="connectionId">Connection identifier</param>
    void Add(IIdentity identity, string connectionId);

    /// <summary>
    /// Remove a connection by its id.
    /// Keyed by connection id (not token) so cleanup works even when the
    /// session was invalidated before the socket closed.
    /// </summary>
    /// <param name="connectionId"></param>
    Task Remove(string connectionId);

    /// <summary>
    /// Get all connected users
    /// </summary>
    /// <returns>Dictionary mapping user IDs to their connection IDs</returns>
    IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers();
}