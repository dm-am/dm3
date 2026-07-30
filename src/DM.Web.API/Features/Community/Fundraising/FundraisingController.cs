using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Fundraising;

/// <summary>
/// API controller for the website fundraising progress
/// </summary>
/// <remarks>
/// The fundraising progress is a single record: everyone can read it,
/// administrators update it in place (no history is kept).
/// </remarks>
[ApiController]
[Route("v1/fundraising")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Fundraising")]
public class FundraisingController : ControllerBase
{
    private readonly IFundraisingApiService _apiService;

    /// <inheritdoc />
    public FundraisingController(
        IFundraisingApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get the current fundraising progress
    /// </summary>
    /// <remarks>
    /// Returns the goal amount, the amount collected so far and the last update moment.
    /// Available anonymously.
    /// </remarks>
    /// <response code="200">Fundraising progress retrieved successfully</response>
    /// <response code="404">Fundraising progress not found</response>
    [HttpGet(Name = nameof(GetFundraising))]
    [ProducesResponseType(typeof(Envelope<Fundraising>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFundraising() => Ok(await _apiService.Get());

    /// <summary>
    /// Update the fundraising progress
    /// </summary>
    /// <remarks>
    /// Requires admin role. Replaces the goal amount and the collected amount.
    /// Goal amount must be greater than 0, collected amount must be 0 or greater.
    /// </remarks>
    /// <param name="request">Fundraising update request</param>
    /// <response code="200">Fundraising progress updated successfully</response>
    /// <response code="400">Some fundraising properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to update the fundraising progress</response>
    [HttpPut(Name = nameof(PutFundraising))]
    [RequireRole(UserRole.Admin)]
    [ProducesResponseType(typeof(Envelope<Fundraising>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PutFundraising([FromBody] UpdateFundraisingRequest request) =>
        Ok(await _apiService.Update(request));
}
