using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// API service for warning management
/// </summary>
public interface IWarningApiService
{
    /// <summary>
    /// Get warnings for a specific user
    /// </summary>
    Task<UserWarningsInfo> GetUserWarnings(string login);

    /// <summary>
    /// Get a page of the warning log, open to everyone
    /// </summary>
    Task<ListEnvelope<WarningLogEntry>> GetWarningLog(WarningsQuery query);

    /// <summary>
    /// Get a page of the warning log with the evidence (for moderators)
    /// </summary>
    Task<ListEnvelope<Warning>> GetAllWarnings(WarningsQuery query);

    /// <summary>
    /// Create a warning
    /// </summary>
    Task<Envelope<Warning>> CreateWarning(CreateWarningRequest request);

    /// <summary>
    /// Change the points of a warning, keeping the warning itself
    /// </summary>
    Task<Envelope<Warning>> ChangeWarningPoints(Guid warningId, ChangeWarningPointsRequest request);

    /// <summary>
    /// Take the picture out of a warning on a profile, for good
    /// </summary>
    Task DeleteProfilePicture(Guid warningId);
}
