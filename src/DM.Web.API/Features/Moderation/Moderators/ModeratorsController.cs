using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Moderators;

/// <summary>
/// Moderation team overview endpoints
/// </summary>
/// <remarks>
/// Provides the data for the moderation page listing all moderators
/// with their zones of responsibility (forum boards, curated games and blogs).
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Moderation")]
public class ModeratorsController : ControllerBase
{
    private readonly IModeratorsApiService _moderatorsApiService;

    /// <inheritdoc />
    public ModeratorsController(IModeratorsApiService moderatorsApiService)
    {
        _moderatorsApiService = moderatorsApiService;
    }

    /// <summary>
    /// Get moderation team with zones of responsibility (moderators only)
    /// </summary>
    /// <remarks>
    /// Returns all users with Moderator role or higher (highest role first,
    /// then by username). Each entry carries the user's zones:
    /// - **boards**: forum boards the user moderates
    /// - **curatedGames**: games the user curates as premoderation mentor
    /// - **curatedBlogs**: blogs the user curates as premoderation mentor
    /// </remarks>
    /// <response code="200">List of moderators with their zones</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Moderator or higher role required</response>
    [HttpGet("moderators", Name = nameof(GetModerators))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<ModeratorOverview>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModerators() =>
        Ok(await _moderatorsApiService.GetModerators());
}
