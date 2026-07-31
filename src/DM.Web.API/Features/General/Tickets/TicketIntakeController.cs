using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.Http;
using DM.Web.API.Shared.RateLimiting;
using DM.Domain.Core.Exceptions;

namespace DM.Web.API.Features.General.Tickets;

/// <summary>
/// API controller for public ticket intake (support/complaint forms)
/// </summary>
/// <remarks>
/// Tickets ("обращения") are submitted via the public /support and /complaint
/// forms. Guests are allowed to submit — losing account access must not
/// lock a user out of support. Moderation reviews the submissions on the
/// role-scoped moderation ticket endpoints.
/// </remarks>
[ApiController]
[Route("v1/tickets")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Tickets")]
public class TicketIntakeController : ControllerBase
{
    private readonly ITicketIntakeApiService _ticketIntakeApiService;

    /// <inheritdoc />
    public TicketIntakeController(ITicketIntakeApiService ticketIntakeApiService)
    {
        _ticketIntakeApiService = ticketIntakeApiService;
    }

    /// <summary>
    /// Create a ticket
    /// </summary>
    /// <remarks>
    /// Creates a new ticket from the /support or /complaint form.
    /// Anonymous submissions are allowed; authenticated authors are recorded
    /// automatically, guests must leave a contact email and receive a tracking
    /// token in the response so they can follow up without an account.
    /// Protected by a honeypot field and rate limiting.
    /// </remarks>
    /// <param name="request">Ticket creation request</param>
    /// <response code="201">Ticket created (with the guest tracking token when applicable)</response>
    /// <response code="400">Validation error</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost(Name = nameof(CreateTicketIntake))]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(CreateTicketIntakeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateTicketIntake([FromBody] CreateTicketIntakeRequest request)
    {
        // Honeypot check for bot protection
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["subject"] = "Не удалось отправить обращение",
            }, RefusalMessage.InvalidData);
        }

        var response = await _ticketIntakeApiService.CreateTicket(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Track a guest ticket
    /// </summary>
    /// <remarks>
    /// Public, token-gated view of a guest ticket: its status and conversation
    /// thread. No authentication — possession of the tracking token returned at
    /// submission time is the credential, which is exactly why it travels in the
    /// X-Dm-Ticket-Token header and not in the path: a path is written verbatim
    /// into the proxy access log and into the trace, so a token placed there is
    /// readable by everyone who can read either. Returns 404 for a missing, empty
    /// or unknown token.
    /// </remarks>
    /// <param name="token">Tracking token issued at submission time, in the X-Dm-Ticket-Token header</param>
    /// <response code="200">Ticket status and thread</response>
    /// <response code="404">No ticket for this token</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpGet("track", Name = nameof(TrackTicketIntake))]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(Envelope<TrackedTicket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> TrackTicketIntake(
        [FromHeader(Name = TokenHeaders.Ticket)] string? token)
    {
        var ticket = await _ticketIntakeApiService.TrackTicket(token ?? string.Empty);
        return ticket != null ? Ok(new Envelope<TrackedTicket>(ticket)) : NotFound();
    }
}
