using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// API service for username change request moderation
/// </summary>
public interface IUsernameChangeApiService
{
    /// <summary>
    /// Get all pending username change requests
    /// </summary>
    Task<IEnumerable<UsernameChangeRequest>> GetPendingRequestsAsync();

    /// <summary>
    /// Get username change request by ID
    /// </summary>
    Task<UsernameChangeRequest> GetByIdAsync(Guid id);

    /// <summary>
    /// Resolve (approve/reject) a username change request
    /// </summary>
    Task<UsernameChangeRequest> ResolveAsync(Guid id, ResolveUsernameChangeRequest resolve);
}
