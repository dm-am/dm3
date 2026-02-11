using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <summary>
/// Service for login change requests
/// </summary>
public interface ILoginChangeService
{
    /// <summary>
    /// Create a login change request
    /// </summary>
    Task<LoginChangeRequestEntry> Create(CreateLoginChangeRequest request);

    /// <summary>
    /// Get current user's latest request
    /// </summary>
    Task<LoginChangeRequestEntry?> GetCurrentUserRequest();

    /// <summary>
    /// Get all pending requests (admin)
    /// </summary>
    Task<IReadOnlyCollection<LoginChangeRequestEntry>> GetPendingRequests();

    /// <summary>
    /// Get request by ID (admin)
    /// </summary>
    Task<LoginChangeRequestEntry> GetById(Guid requestId);

    /// <summary>
    /// Resolve (approve/reject) a request (admin)
    /// </summary>
    Task<LoginChangeRequestEntry> Resolve(ResolveLoginChangeRequest resolve);
}
