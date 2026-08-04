using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// API service for the violators list
/// </summary>
public interface IViolatorApiService
{
    /// <summary>
    /// Get violators: users with active warning points or an active ban
    /// </summary>
    /// <param name="banState">Ban state: all | banned | points-only</param>
    Task<ListEnvelope<Violator>> GetViolators(string banState);
}
