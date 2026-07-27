using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Web.API.Realtime;

/// <summary>
/// SignalR connected users service
/// </summary>
/// <remarks>
/// Tracks authenticated connections only: registration requires a valid
/// auth token, so anonymous (guest) connections never appear here and can
/// never be targeted by per-user events.
/// </remarks>
public interface IUserConnectionService
{
    /// <summary>
    /// Authenticate user and save its connection.
    /// No-op when the token does not resolve to an authenticated identity.
    /// </summary>
    /// <param name="authToken"></param>
    /// <param name="connectionId"></param>
    Task Add(string authToken, string connectionId);

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