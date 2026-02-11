using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <summary>
/// Repository for login change request operations
/// </summary>
internal interface ILoginChangeRepository
{
    /// <summary>
    /// Get pending request for user
    /// </summary>
    Task<LoginChangeRequest?> GetPendingByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get latest request for user (any status)
    /// </summary>
    Task<LoginChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get request by ID
    /// </summary>
    Task<LoginChangeRequest?> GetById(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Get all pending requests
    /// </summary>
    Task<IReadOnlyCollection<LoginChangeRequestEntry>> GetPendingRequests(CancellationToken ct = default);

    /// <summary>
    /// Create a new request
    /// </summary>
    Task Add(LoginChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Save changes
    /// </summary>
    Task SaveChanges(CancellationToken ct = default);
}
