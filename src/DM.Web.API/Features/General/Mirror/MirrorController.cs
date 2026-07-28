using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.General.Mirror;

/// <summary>
/// Controller for site mirrors management
/// </summary>
/// <remarks>
/// Exposes the configured mirrors so the client can offer a region switch.
/// Carrying a session across mirrors is not supported: a mirror authenticates
/// its visitors itself.
/// </remarks>
[ApiController]
[Route("v1/mirrors")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Mirrors")]
public class MirrorController : ControllerBase
{
    private readonly MirrorConfiguration _config;

    /// <summary>
    /// Creates a new instance of MirrorController
    /// </summary>
    public MirrorController(IOptions<MirrorConfiguration> config)
    {
        _config = config.Value;
    }

    /// <summary>
    /// Get list of available mirrors
    /// </summary>
    /// <remarks>
    /// Returns only mirrors that have configured WebUrl (are ready to use).
    /// </remarks>
    /// <response code="200">List of available mirrors</response>
    [HttpGet(Name = nameof(GetMirrors))]
    [ProducesResponseType(typeof(MirrorsResponse), StatusCodes.Status200OK)]
    public ActionResult<MirrorsResponse> GetMirrors()
    {
        var mirrors = _config.Mirrors
            .Where(m => !string.IsNullOrEmpty(m.Value.WebUrl))
            .Select(m => new MirrorDto
            {
                Id = m.Value.Id,
                Name = m.Value.Name,
                WebUrl = m.Value.WebUrl!,
                IsCurrent = m.Key == _config.CurrentMirrorId
            })
            .ToList();

        return Ok(new MirrorsResponse
        {
            CurrentMirrorId = _config.CurrentMirrorId,
            Mirrors = mirrors
        });
    }
}

/// <summary>
/// Response with available mirrors
/// </summary>
public record MirrorsResponse
{
    /// <summary>
    /// Current mirror ID
    /// </summary>
    public required string CurrentMirrorId { get; init; }

    /// <summary>
    /// List of available mirrors
    /// </summary>
    public required List<MirrorDto> Mirrors { get; init; }
}

/// <summary>
/// Mirror information
/// </summary>
public record MirrorDto
{
    /// <summary>
    /// Mirror ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Mirror name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Web URL
    /// </summary>
    public required string WebUrl { get; init; }

    /// <summary>
    /// Whether this is the current mirror
    /// </summary>
    public bool IsCurrent { get; init; }
}
