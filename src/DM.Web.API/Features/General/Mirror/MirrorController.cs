using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Core.Configuration;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.General.Mirror;

/// <summary>
/// Controller for site mirrors management
/// </summary>
/// <remarks>
/// Manages site mirror configuration. Session transfer between mirrors
/// is supported via transfer tokens (valid for 5 minutes).
/// </remarks>
[ApiController]
[Route("v1/mirrors")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Mirrors")]
public class MirrorController : ControllerBase
{
    private readonly MirrorConfiguration _config;
    private readonly IAuthenticationService _authService;

    /// <summary>
    /// Creates a new instance of MirrorController
    /// </summary>
    public MirrorController(
        IOptions<MirrorConfiguration> config,
        IAuthenticationService authService)
    {
        _config = config.Value;
        _authService = authService;
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
    /// Returns a URL to the target mirror with a session transfer token.
    /// The token is valid for 5 minutes and allows seamless authentication on the target mirror.
    /// </remarks>
    /// <param name="targetMirror">Target mirror ID</param>
    /// <param name="returnUrl">Optional URL to redirect to after transfer</param>
    /// <response code="200">Transfer URL with optional session token</response>
    /// <response code="400">Invalid or unavailable mirror</response>
    [HttpGet("transfer", Name = nameof(GetTransferToken))]
    [ProducesResponseType(typeof(TransferResponse), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    public async Task<ActionResult<TransferResponse>> GetTransferToken(
        [FromQuery] string targetMirror,
        [FromQuery] string? returnUrl = null)
    {
        if (!_config.Mirrors.TryGetValue(targetMirror, out var target) ||
            string.IsNullOrEmpty(target.WebUrl))
        {
            throw new HttpException(HttpStatusCode.BadRequest, "Invalid or unavailable mirror");
        }

        var transferToken = await _authService.CreateTransferToken();

        var transferUrl = target.WebUrl;
        if (!string.IsNullOrEmpty(returnUrl))
        {
            transferUrl += returnUrl;
        }

        // If user is authenticated, append transfer token
        if (!string.IsNullOrEmpty(transferToken))
        {
            var separator = transferUrl.Contains('?') ? "&" : "?";
            transferUrl += $"{separator}transfer={Uri.EscapeDataString(transferToken)}";
        }

        return Ok(new TransferResponse { TransferUrl = transferUrl });
    }

    /// <summary>
    /// Accept transfer token from another mirror
    /// </summary>
    /// <remarks>
    /// Validates the transfer token and returns authentication credentials for this mirror.
    /// The frontend should call this when it detects a transfer token in the URL.
    /// </remarks>
    /// <param name="transferToken">Transfer token from source mirror</param>
    /// <response code="200">Authentication result with token</response>
    /// <response code="400">Invalid or expired transfer token</response>
    [HttpPost("transfer/accept", Name = nameof(AcceptTransfer))]
    [ProducesResponseType(typeof(TransferAcceptResponse), 200)]
    [ProducesResponseType(typeof(ErrorEnvelope), 400)]
    public async Task<ActionResult<TransferAcceptResponse>> AcceptTransfer([FromQuery] string transferToken)
    {
        var identity = await _authService.AuthenticateWithTransferToken(transferToken);

        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.BadRequest, "Invalid or expired transfer token");
        }

        return Ok(new TransferAcceptResponse
        {
            AuthToken = identity.AuthenticationToken!,
            Username = identity.User.Username
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

/// <summary>
/// Transfer token response
/// </summary>
public record TransferResponse
{
    /// <summary>
    /// URL to redirect to for switching mirror (includes transfer token if authenticated)
    /// </summary>
    public string? TransferUrl { get; init; }
}

/// <summary>
/// Transfer acceptance response
/// </summary>
public record TransferAcceptResponse
{
    /// <summary>
    /// Authentication token to use on this mirror
    /// </summary>
    public required string AuthToken { get; init; }

    /// <summary>
    /// Username of the authenticated user
    /// </summary>
    public required string Username { get; init; }
}
