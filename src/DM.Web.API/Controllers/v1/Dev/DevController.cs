using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Dev;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Dev;

/// <summary>
/// Development-only endpoints for testing
/// </summary>
[ApiController]
[Route("v1/dev")]
[ApiExplorerSettings(GroupName = "Dev")]
public class DevController : ControllerBase
{
    private readonly IDevApiService _devApiService;

    /// <inheritdoc />
    public DevController(IDevApiService devApiService)
    {
        _devApiService = devApiService;
    }

    /// <summary>
    /// Set role for current user (dev only)
    /// </summary>
    /// <param name="role">New role</param>
    /// <response code="204">Role changed successfully</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost("role/{role}")]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> SetRole(UserRole role)
    {
        await _devApiService.SetRole(role);
        return NoContent();
    }

    /// <summary>
    /// Get all users from database
    /// </summary>
    /// <response code="200">List of all users</response>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(IReadOnlyList<TestAccountInfo>), 200)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _devApiService.GetAllUsers();
        return Ok(users);
    }
}
