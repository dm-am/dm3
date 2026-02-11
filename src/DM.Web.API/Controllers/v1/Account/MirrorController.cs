using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Configuration;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// Controller for site mirrors management
/// </summary>
/// <remarks>
/// Manages site mirror configuration. Currently, session transfer between mirrors
/// is not implemented - users need to log in again when switching mirrors.
/// </remarks>
[ApiController]
[Route("v1/[controller]")]
[ApiExplorerSettings(GroupName = "Account")]
[Tags("Mirror")]
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
    [ProducesResponseType(typeof(MirrorsResponse), 200)]
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

    /// <summary>
    /// Get transfer URL for switching to another mirror
    /// </summary>
    /// <remarks>
    /// Returns a URL to the target mirror. Note: session transfer is not currently
    /// implemented - users will need to log in again on the new mirror.
    /// </remarks>
    /// <param name="targetMirror">Target mirror ID</param>
    /// <param name="returnUrl">Optional URL to redirect to after transfer</param>
    /// <response code="200">Transfer URL</response>
    /// <response code="400">Invalid or unavailable mirror</response>
    [HttpGet("transfer", Name = nameof(GetTransferToken))]
    [ProducesResponseType(typeof(TransferResponse), 200)]
    [ProducesResponseType(typeof(GeneralError), 400)]
    public ActionResult<TransferResponse> GetTransferToken(
        [FromQuery] string targetMirror,
        [FromQuery] string? returnUrl = null)
    {
        if (!_config.Mirrors.TryGetValue(targetMirror, out var target) ||
            string.IsNullOrEmpty(target.WebUrl))
        {
            return BadRequest(new GeneralError("Invalid or unavailable mirror"));
        }

        // Simple redirect without session transfer (stub)
        // TODO: Implement session transfer when needed
        var transferUrl = target.WebUrl;
        if (!string.IsNullOrEmpty(returnUrl))
        {
            transferUrl += returnUrl;
        }

        return Ok(new TransferResponse { TransferUrl = transferUrl });
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

/// <summary>
/// Transfer token response
/// </summary>
public record TransferResponse
{
    /// <summary>
    /// URL to redirect to for switching mirror
    /// </summary>
    public string? TransferUrl { get; init; }
}
