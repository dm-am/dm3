using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Users.LoginHistory;

/// <summary>
/// Repository for login history operations
/// </summary>
internal interface ILoginHistoryRepository
{
    /// <summary>
    /// Get login history for a user
    /// </summary>
    Task<IReadOnlyCollection<LoginHistoryEntry>> GetByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Check if a login is reserved in history (cannot be reused)
    /// </summary>
    Task<bool> IsLoginReserved(string login, CancellationToken ct = default);

    /// <summary>
    /// Record a login change
    /// </summary>
    Task Add(DataAccess.BusinessObjects.Users.LoginHistory entry, CancellationToken ct = default);
}
