using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// Aggregated moderation profile endpoint
/// </summary>
/// <remarks>
/// Provides a single endpoint that returns all moderation-related data for a user profile.
/// Fields are filtered server-side based on the caller's role:
///
/// - **Admin**: all fields (email, IP addresses, login history, linked profiles, notes, violations)
/// - **SeniorModerator**: linked profiles, notes, violations (can issue bans)
/// - **Moderator**: linked profiles, notes, violations (can issue warnings)
///
/// The response includes a `permissions` object that tells the frontend which UI elements to show.
/// </remarks>
[ApiController]
[Route("v1/moderation/users")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("ModerationProfile")]
public class ModerationProfileController : ControllerBase
{
    private readonly IModerationProfileApiService _service;

    /// <inheritdoc />
    public ModerationProfileController(IModerationProfileApiService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get moderation profile for a user
    /// </summary>
    /// <remarks>
    /// Returns aggregated moderation data including linked profiles, moderator notes,
    /// violation summary, and (for admins) IP addresses and login history.
    ///
    /// Use the `permissions` field in the response to determine which actions
    /// the current user can perform and which UI sections to display.
    /// </remarks>
    /// <param name="login">Target user login</param>
    /// <response code="200">Moderation profile (fields filtered by caller role)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role or higher required</response>
    /// <response code="404">User not found</response>
    [HttpGet("{login}/profile", Name = nameof(GetModerationProfile))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<ModerationProfileDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetModerationProfile(string login) =>
        Ok(await _service.GetModerationProfile(login));
}
