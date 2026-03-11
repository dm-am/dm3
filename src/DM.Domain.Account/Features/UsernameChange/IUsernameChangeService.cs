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
    Task<UsernameChangeRequestEntry> CreateAsync(CreateUsernameChangeRequest request);

    /// <summary>
    /// Get current user's latest request
    /// </summary>
    Task<UsernameChangeRequestEntry?> GetCurrentUserRequestAsync();

    /// <summary>
    /// Get all pending requests (admin)
    /// </summary>
    Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequestsAsync();

    /// <summary>
    /// Get request by ID (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> GetByIdAsync(Guid requestId);

    /// <summary>
    /// Resolve (approve/reject) a request (admin)
    /// </summary>
    Task<UsernameChangeRequestEntry> ResolveAsync(ResolveUsernameChangeRequest resolve);

    /// <summary>
    /// Get request by approval token (for user completing the change)
    /// </summary>
    Task<UsernameChangeRequestEntry?> GetByApprovalTokenAsync(Guid token);

    /// <summary>
    /// Complete username change using approval token
    /// </summary>
    Task<UsernameChangeRequestEntry> CompleteWithTokenAsync(Guid token, string newUsername);

    /// <summary>
    /// Rollback a completed username change (moderator action)
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <returns>Updated request entry</returns>
    Task<UsernameChangeRequestEntry> RollbackAsync(Guid requestId);
}
