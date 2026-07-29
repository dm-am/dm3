using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// API service for user endorsement operations
/// </summary>
public interface IUserEndorsementApiService
{
    /// <summary>
    /// Get endorsements written about a user
    /// </summary>
    /// <param name="username">Endorsement recipient's username</param>
    /// <param name="query">Search, sorting and paging</param>
    Task<ListEnvelope<UserEndorsement>> GetReceived(string username, UserEndorsementsQuery query);

    /// <summary>
    /// Get endorsements written by a user
    /// </summary>
    /// <param name="username">Endorsement author's username</param>
    /// <param name="query">Search, sorting and paging</param>
    Task<ListEnvelope<UserEndorsement>> GetWritten(string username, UserEndorsementsQuery query);

    /// <summary>
    /// Get a single endorsement
    /// </summary>
    /// <param name="id">Endorsement identifier</param>
    Task<UserEndorsement> Get(Guid id);

    /// <summary>
    /// Endorse a user
    /// </summary>
    /// <param name="username">Username of the user to endorse</param>
    /// <param name="request">Endorsement data</param>
    Task<UserEndorsement> Create(string username, CreateUserEndorsementRequest request);

    /// <summary>
    /// Update an existing endorsement
    /// </summary>
    /// <param name="id">Endorsement identifier</param>
    /// <param name="request">Update data</param>
    Task<UserEndorsement> Update(Guid id, UpdateUserEndorsementRequest request);

    /// <summary>
    /// Delete an existing endorsement
    /// </summary>
    /// <param name="id">Endorsement identifier</param>
    Task Delete(Guid id);
}
