using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Core.Users;

/// <summary>
/// Read-only interface for username history access.
/// Used by Community module to display username history in profiles.
/// </summary>
public interface IUsernameHistoryReader
{
    /// <summary>
    /// Get username history for a user
    /// </summary>
    Task<IReadOnlyCollection<UsernameHistoryEntry>> GetByUserId(Guid userId, CancellationToken ct = default);
}
