using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Service for username change requests
/// </summary>
public interface IUsernameChangeService
{
    /// <summary>
    /// Create a username change request
    /// </summary>
    Task<UsernameChangeRequestEntry> Create(CreateUsernameChangeRequest request);

    /// <summary>
    /// Get current user's latest request
    /// </summary>
    Task<UsernameChangeRequestEntry?> GetCurrentUserRequest();

    /// <summary>
    /// Get all pending requests (admin)
    /// </summary>
    Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequests();

    /// <summary>
    /// Get request by ID (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> GetById(Guid requestId);

    /// <summary>
    /// Resolve (approve/reject) a request (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> Resolve(ResolveUsernameChangeRequest resolve);

    /// <summary>
    /// Get request by approval token (for user completing the change)
    /// </summary>
    Task<UsernameChangeRequestEntry?> GetByApprovalToken(Guid token);

    /// <summary>
    /// Complete username change using approval token
    /// </summary>
    Task<UsernameChangeRequestEntry> CompleteWithToken(Guid token, string newUsername);

    /// <summary>
    /// Rollback a completed username change (moderator action)
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <returns>Updated request entry</returns>
    Task<UsernameChangeRequestEntry> Rollback(Guid requestId);
}
