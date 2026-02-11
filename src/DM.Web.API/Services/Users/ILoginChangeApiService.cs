using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for login change requests
/// </summary>
public interface ILoginChangeApiService
{
    /// <summary>
    /// Create a login change request
    /// </summary>
    /// <param name="request">Request data</param>
    /// <returns>Created request</returns>
    Task<Envelope<LoginChangeRequestDto>> Create(CreateLoginChangeRequestDto request);

    /// <summary>
    /// Get current user's latest login change request
    /// </summary>
    /// <returns>Latest request or null if none exists</returns>
    Task<Envelope<LoginChangeRequestDto>?> GetCurrentUserRequest();

    /// <summary>
    /// Get all pending login change requests (moderator)
    /// </summary>
    /// <returns>List of pending requests</returns>
    Task<ListEnvelope<LoginChangeRequestDto>> GetPendingRequests();

    /// <summary>
    /// Get login change request by ID (moderator)
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <returns>Request details</returns>
    Task<Envelope<LoginChangeRequestDto>> GetById(Guid requestId);

    /// <summary>
    /// Resolve (approve/reject) a login change request (moderator)
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="resolve">Resolution data</param>
    /// <returns>Updated request</returns>
    Task<Envelope<LoginChangeRequestDto>> Resolve(Guid requestId, ResolveLoginChangeRequestDto resolve);
}
