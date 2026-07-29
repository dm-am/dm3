using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// API service for granting and revoking user awards
/// </summary>
public interface IUserAwardApiService
{
    /// <summary>
    /// Grant an award to a user
    /// </summary>
    /// <param name="username">Recipient's username</param>
    /// <param name="request">Award type and optional contest series</param>
    Task<Envelope<UserAward>> Grant(string username, GrantUserAwardRequest request);

    /// <summary>
    /// Revoke a previously granted award (soft-delete)
    /// </summary>
    /// <param name="awardId">Grant record identifier</param>
    Task Revoke(Guid awardId);
}
