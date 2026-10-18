using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Shared.RateLimiting;
using DM.Web.API.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// API controller for moderation tickets (user reports)
/// </summary>
/// <remarks>
/// Tickets allow users to report content violations to moderators. A ticket is a
/// conversation between its author and moderation, and moderation closes it with
/// an answer. Warnings and bans are issued from the profile of the violator, not
/// from a ticket.
/// </remarks>
[ApiController]
[Route("v1/moderation/tickets")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Tickets")]
public class TicketController : ControllerBase
{
    private readonly ITicketApiService _ticketApiService;

    /// <inheritdoc />
    public TicketController(ITicketApiService ticketApiService)
    {
        _ticketApiService = ticketApiService;
    }

    /// <summary>
    /// Get all tickets
    /// </summary>
    /// <remarks>
    /// Returns the queue: the moderation tickets visible to the caller role, the
    /// ones waiting for moderation first, then those waiting for their author,
    /// then the closed and the spam, each group newest first. Requires Moderator
    /// role or higher. Moderator sees user complaints and suggestions,
    /// SeniorModerator additionally sees complaints about junior moderator
    /// decisions, Admin sees all categories. A complaint that names the caller as
    /// the violator is not in his queue. Can be filtered by status and subtype.
    /// </remarks>
    /// <param name="status">Optional status filter</param>
    /// <param name="subtype">Optional subtype filter (within the caller visibility scope)</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of tickets</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetTickets))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] PagingQuery q,
        [FromQuery] TicketStatus? status = null, [FromQuery] TicketSubtype? subtype = null) =>
        Ok(await _ticketApiService.GetTickets(q, status, subtype));

    /// <summary>
    /// Get ticket statistics
    /// </summary>
    /// <remarks>
    /// Returns count of tickets grouped by status.
    /// </remarks>
    /// <response code="200">Ticket statistics</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet("stats", Name = nameof(GetTicketStats))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(TicketStats), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketStats() =>
        Ok(await _ticketApiService.GetStats());

    /// <summary>
    /// Get my filed tickets
    /// </summary>
    /// <remarks>
    /// Returns tickets filed by the current user. Can be filtered by status and
    /// subtype server-side.
    /// </remarks>
    /// <param name="q">Paging parameters</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="subtype">Optional subtype filter</param>
    /// <response code="200">List of filed tickets</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("mine", Name = nameof(GetMyFiledTickets))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFiledTickets(
        [FromQuery] PagingQuery q,
        [FromQuery] TicketStatus? status = null, [FromQuery] TicketSubtype? subtype = null) =>
        Ok(await _ticketApiService.GetMyFiledTickets(q, status, subtype));

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    /// <remarks>
    /// Returns a ticket with its conversation, and what the caller may do in it.
    /// The author reads his own ticket; a moderator reads a ticket of a category
    /// his role sees, unless the ticket names him as the violator. Anybody else is
    /// answered 404, the same as for a ticket that does not exist.
    /// </remarks>
    /// <param name="ticketId">Ticket ID</param>
    /// <response code="200">Ticket details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Ticket not found</response>
    [HttpGet("{ticketId:guid}", Name = nameof(GetTicket))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<TicketDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(Guid ticketId) =>
        Ok(await _ticketApiService.GetTicket(ticketId));

    /// <summary>
    /// Create a ticket (report content)
    /// </summary>
    /// <remarks>
    /// Creates a new moderation ticket to report a user or content.
    /// Any authenticated user can create tickets.
    /// </remarks>
    /// <param name="request">Ticket creation request</param>
    /// <response code="201">Ticket created</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Target user not found</response>
    [HttpPost(Name = nameof(CreateTicket))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Ticket>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var ticket = await _ticketApiService.CreateTicket(request);
        return CreatedAtRoute(nameof(GetTicket), new { ticketId = ticket.Resource.Id }, ticket);
    }

    /// <summary>
    /// Answer in the conversation of a ticket
    /// </summary>
    /// <remarks>
    /// The author of the ticket and the moderators who read it answer in it while
    /// it is open. The answer of the author leaves the ticket waiting for
    /// moderation, and he sends no status (403 if he does, whatever his role). A
    /// moderator chooses with his answer the status the ticket moves to: waiting
    /// for the author when left out, closed, or spam; for the last two the text
    /// may stay empty, and the answer then only changes the status. The text and
    /// the status are written together or not at all. An answer is not edited or
    /// deleted afterwards. The conversation takes a hundred answers. The ticket
    /// comes back as it stands after the answer.
    /// </remarks>
    /// <param name="ticketId">Ticket ID</param>
    /// <param name="request">The answer</param>
    /// <response code="200">The ticket with its conversation, after the answer</response>
    /// <response code="400">The ticket is closed or full, or the text or the status is refused</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">The author of the ticket sent a status</response>
    /// <response code="404">Ticket not found</response>
    /// <response code="429">Too many requests. Try again later.</response>
    [HttpPost("{ticketId}/comments", Name = nameof(CreateTicketComment))]
    [AuthenticationRequired]
    [EnableRateLimiting(RateLimitPolicies.Default)]
    [ProducesResponseType(typeof(Envelope<TicketDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateTicketComment(Guid ticketId, [FromBody] AnswerTicketRequest request) =>
        Ok(await _ticketApiService.CreateComment(ticketId, request));
}
