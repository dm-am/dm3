using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Web.API.Realtime;

/// <summary>
/// SignalR connected users service
/// </summary>
public interface IUserConnectionService
{
    /// <summary>
    /// Authenticate user and save its connection
    /// </summary>
    /// <param name="authToken"></param>
    /// <param name="connectionId"></param>
    Task Add(string authToken, string connectionId);

    /// <summary>
    /// Authenticate user and remove its connection
    /// </summary>
    /// <param name="authToken"></param>
    /// <param name="connectionId"></param>
    Task Remove(string authToken, string connectionId);

    /// <summary>
    /// Get all connected users
    /// </summary>
    /// <returns>Dictionary mapping user IDs to their connection IDs</returns>
    IReadOnlyDictionary<Guid, IEnumerable<string>> GetConnectedUsers();
}