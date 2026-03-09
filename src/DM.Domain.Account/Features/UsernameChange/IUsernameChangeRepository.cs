using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Repository for username change request operations
/// </summary>
public interface IUsernameChangeRepository
{
    /// <summary>
    /// Get pending request for user
    /// </summary>
    Task<UsernameChangeRequest?> GetPendingByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get latest request for user (any status)
    /// </summary>
    Task<UsernameChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get request by ID
    /// </summary>
    Task<UsernameChangeRequest?> GetById(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Get all pending requests
    /// </summary>
    Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequests(CancellationToken ct = default);

    /// <summary>
    /// Get request by approval token
    /// </summary>
    Task<UsernameChangeRequest?> GetByApprovalToken(Guid token, CancellationToken ct = default);

    /// <summary>
    /// Create a new request
    /// </summary>
    Task Add(UsernameChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update an existing request
    /// </summary>
    Task Update(UsernameChangeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update user's username
    /// </summary>
    Task UpdateUserUsername(Guid userId, string newUsername, CancellationToken ct = default);

    /// <summary>
    /// Check if username is available (not taken by another user)
    /// </summary>
    Task<bool> IsUsernameAvailable(string username, Guid? excludeUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Save changes
    /// </summary>
    Task SaveChanges(CancellationToken ct = default);
}
