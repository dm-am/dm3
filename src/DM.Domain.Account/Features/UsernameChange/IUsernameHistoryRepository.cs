using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Users;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Repository for username history operations.
/// Inherits read-only operations from IUsernameHistoryReader.
/// </summary>
public interface IUsernameHistoryRepository : IUsernameHistoryReader
{
    /// <summary>
    /// Check if a username is reserved in history (cannot be reused by anyone)
    /// </summary>
    Task<bool> IsUsernameReserved(string username, CancellationToken ct = default);

    /// <summary>
    /// Check if a username is reserved for others (excluding the specified user)
    /// </summary>
    Task<bool> IsUsernameReservedForOthers(string username, Guid excludeUserId, CancellationToken ct = default);

    /// <summary>
    /// Get the latest (most recent) username change for a user
    /// </summary>
    Task<UsernameHistoryEntry?> GetLatestByUserId(Guid userId, CancellationToken ct = default);
}
