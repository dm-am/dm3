using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Moderation;

namespace DM.Web.API.Services.Moderation;

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
    /// Get all warnings (for moderators)
    /// </summary>
    Task<ListEnvelope<Warning>> GetAllWarnings(string? userLogin = null);

    /// <summary>
    /// Create a warning
    /// </summary>
    Task<Envelope<Warning>> CreateWarning(CreateWarningRequest request);

    /// <summary>
    /// Remove (deactivate) a warning
    /// </summary>
    Task RemoveWarning(Guid warningId);
}
