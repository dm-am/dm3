using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Fundraising;

/// <summary>
/// API service for the website fundraising progress
/// </summary>
public interface IFundraisingApiService
{
    /// <summary>
    /// Get the current fundraising progress
    /// </summary>
    /// <returns>Envelope of fundraising progress</returns>
    Task<Envelope<Fundraising>> Get();

    /// <summary>
    /// Update the fundraising progress
    /// </summary>
    /// <param name="request">Fundraising update request</param>
    /// <returns>Envelope of updated fundraising progress</returns>
    Task<Envelope<Fundraising>> Update(UpdateFundraisingRequest request);
}
