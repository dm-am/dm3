using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Moderation;
using DM.Web.API.Services.Moderation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Moderation;

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
    /// Returns all moderation tickets. Requires Moderator role or higher.
    /// Can be filtered by status.
    /// </remarks>
    /// <param name="status">Optional status filter</param>
    /// <response code="200">List of tickets</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Moderator role required</response>
    [HttpGet(Name = nameof(GetTickets))]
    [RequireRole(UserRole.Moderator)]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetTickets([FromQuery] TicketStatus? status = null) =>
        Ok(await _ticketApiService.GetTickets(status));

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
    [ProducesResponseType(typeof(TicketStats), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
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
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetMyAssignedTickets() =>
        Ok(await _ticketApiService.GetMyAssignedTickets());

    /// <summary>
    /// Get my filed tickets
    /// </summary>
    /// <remarks>
    /// Returns tickets filed by the current user.
    /// </remarks>
    /// <response code="200">List of filed tickets</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("mine", Name = nameof(GetMyFiledTickets))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Ticket>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMyFiledTickets() =>
        Ok(await _ticketApiService.GetMyFiledTickets());

    /// <summary>
    /// Get ticket by ID
    /// </summary>
    /// <remarks>
    /// Returns a specific ticket. Moderators see full details, reporters see their own tickets.
    /// </remarks>
    /// <param name="ticketId">Ticket ID</param>
    /// <response code="200">Ticket details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Ticket not found</response>
    [HttpGet("{ticketId:guid}", Name = nameof(GetTicket))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Ticket>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(Envelope<Ticket>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        var ticket = await _ticketApiService.CreateTicket(request);
        return CreatedAtRoute(nameof(GetTicket), new { ticketId = ticket.Resource.TicketId }, ticket);
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
    [ProducesResponseType(typeof(Envelope<Ticket>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [ProducesResponseType(typeof(Envelope<Ticket>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> ResolveTicket(Guid ticketId, [FromBody] ResolveTicketRequest request) =>
        Ok(await _ticketApiService.ResolveTicket(ticketId, request));
}
