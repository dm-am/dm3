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
    Task<IEnumerable<UsernameChangeRequest>> GetPendingRequests();

    /// <summary>
    /// Get username change request by ID
    /// </summary>
    Task<UsernameChangeRequest> GetById(Guid id);

    /// <summary>
    /// Resolve (approve/reject) a username change request
    /// </summary>
    Task<UsernameChangeRequest> Resolve(Guid id, ResolveUsernameChangeRequest resolve);
}
