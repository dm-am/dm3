using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// Login change requests for current user
/// </summary>
/// <remarks>
/// Allows authenticated users to request a login (username) change.
/// Requests must be reviewed and approved by senior moderators.
/// </remarks>
[ApiController]
[Route("v1/account/login-change")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Login Change")]
[AuthenticationRequired]
public class LoginChangeController : ControllerBase
{
    private readonly ILoginChangeApiService _loginChangeApiService;

    /// <inheritdoc />
    public LoginChangeController(ILoginChangeApiService loginChangeApiService)
    {
        _loginChangeApiService = loginChangeApiService;
    }

    /// <summary>
    /// Create a login change request
    /// </summary>
    /// <remarks>
    /// Creates a new login change request for the authenticated user.
    /// The request will be reviewed by senior moderators.
    /// Users can only have one pending request at a time.
    /// </remarks>
    /// <param name="request">Login change request details</param>
    /// <response code="200">Request created successfully</response>
    /// <response code="400">Invalid request (e.g., login already taken, already have pending request)</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(CreateLoginChangeRequest))]
    [ProducesResponseType(typeof(Envelope<LoginChangeRequestDto>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> CreateLoginChangeRequest([FromBody] CreateLoginChangeRequestDto request) =>
        Ok(await _loginChangeApiService.Create(request));

    /// <summary>
    /// Get current user's latest login change request
    /// </summary>
    /// <remarks>
    /// Returns the authenticated user's most recent login change request if one exists.
    /// </remarks>
    /// <response code="200">Request retrieved successfully (may be null if no request exists)</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMyLoginChangeRequest))]
    [ProducesResponseType(typeof(Envelope<LoginChangeRequestDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMyLoginChangeRequest()
    {
        var result = await _loginChangeApiService.GetCurrentUserRequest();
        return result != null ? Ok(result) : NoContent();
    }
}
