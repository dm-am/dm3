using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// API controller for moderation tickets (user reports)
/// </summary>
/// <remarks>
/// Tickets allow users to report content violations to moderators.
/// Moderators can review, assign, and resolve tickets with optional warnings/bans.
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
    /// Returns moderation tickets visible to the caller role. Requires Moderator
    /// role or higher. Moderator sees user complaints and suggestions,
    /// SeniorModerator additionally sees complaints about junior moderator
    /// decisions, Admin sees all categories. Can be filtered by status and subtype.
    /// </remarks>
    /// <param name="status">Optional status filter</param>
    /// <param name="subtype">Optional subtype filter (within the caller visibility scope)</param>
    /// <response code="200">List of tickets</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetTickets))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] TicketStatus? status = null, [FromQuery] TicketSubtype? subtype = null) =>
        Ok(await _ticketApiService.GetTickets(status, subtype));

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
    /// Get my assigned tickets
    /// </summary>
    /// <remarks>
    /// Returns tickets assigned to the current moderator.
    /// </remarks>
    /// <response code="200">List of assigned tickets</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet("assigned", Name = nameof(GetMyAssignedTickets))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyAssignedTickets() =>
        Ok(await _ticketApiService.GetMyAssignedTickets());

    /// <summary>
    /// Get my filed tickets
    /// </summary>
    /// <remarks>
    /// Returns tickets filed by the current user. Can be filtered by status and
    /// subtype server-side.
    /// </remarks>
    /// <param name="status">Optional status filter</param>
    /// <param name="subtype">Optional subtype filter</param>
    /// <response code="200">List of filed tickets</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("mine", Name = nameof(GetMyFiledTickets))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFiledTickets(
        [FromQuery] TicketStatus? status = null, [FromQuery] TicketSubtype? subtype = null) =>
        Ok(await _ticketApiService.GetMyFiledTickets(status, subtype));

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    /// <remarks>
    /// Returns a specific ticket with the conversation thread (responses).
    /// Moderators see full details, reporters see their own tickets.
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
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        var ticket = await _ticketApiService.GetTicket(ticketId);
        return ticket != null ? Ok(ticket) : NotFound();
    }

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
    /// Assign ticket to me
    /// </summary>
    /// <remarks>
    /// Assigns an open ticket to the current moderator and changes status to InProgress.
    /// </remarks>
    /// <param name="ticketId">Ticket ID</param>
    /// <response code="200">Ticket assigned</response>
    /// <response code="400">Ticket is not in Open status</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Ticket not found</response>
    [HttpPost("{ticketId:guid}/assign", Name = nameof(AssignTicketToMe))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTicketToMe(Guid ticketId) =>
        Ok(await _ticketApiService.AssignToMe(ticketId));

    /// <summary>
    /// Resolve ticket
    /// </summary>
    /// <remarks>
    /// Resolves a ticket with optional warning and/or ban.
    /// </remarks>
    /// <param name="ticketId">Ticket ID</param>
    /// <param name="request">Resolution request</param>
    /// <response code="200">Ticket resolved</response>
    /// <response code="400">Ticket is already closed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    /// <response code="404">Ticket not found</response>
    [HttpPost("{ticketId:guid}/resolve", Name = nameof(ResolveTicket))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(Envelope<Ticket>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveTicket(Guid ticketId, [FromBody] ResolveTicketRequest request) =>
        Ok(await _ticketApiService.ResolveTicket(ticketId, request));
}
