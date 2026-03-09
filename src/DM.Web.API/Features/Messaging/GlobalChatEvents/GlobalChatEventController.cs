using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

/// <summary>
/// Controller for managing chat events in global chat
/// </summary>
/// <remarks>
/// Chat events allow organizing special activities in the global chat.
/// Events can be open (anyone can participate) or closed (organizer manages participants).
/// During a closed event, only participants can send messages to the global chat.
/// </remarks>
[ApiController]
[Route("v1/globalchat/events")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Global Chat")]
public class GlobalChatEventController : ControllerBase
{
    private readonly IGlobalChatEventApiService _apiService;

    /// <inheritdoc />
    public GlobalChatEventController(IGlobalChatEventApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of chat events (upcoming and live)
    /// </summary>
    /// <response code="200">List of events</response>
    [HttpGet(Name = nameof(GetGlobalChatEvents))]
    [ProducesResponseType(typeof(ListEnvelope<GlobalChatEventSummary>), 200)]
    public async Task<IActionResult> GetGlobalChatEvents(CancellationToken ct) =>
        Ok(await _apiService.GetList(ct));

    /// <summary>
    /// Get chat event details
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Event details</response>
    /// <response code="404">Event not found</response>
    [HttpGet("{id}", Name = nameof(GetGlobalChatEvent))]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGlobalChatEvent(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Get(id, ct));

    /// <summary>
    /// Get currently active (Live) event
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Active event or null</response>
    [HttpGet("active", Name = nameof(GetActiveGlobalChatEvent))]
    [ProducesResponseType(typeof(Envelope<GlobalChatEventSummary>), 200)]
    public async Task<IActionResult> GetActiveGlobalChatEvent(CancellationToken ct) =>
        Ok(await _apiService.GetActive(ct));

    /// <summary>
    /// Create a new chat event
    /// </summary>
    /// <param name="input">Event data</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="201">Created event</response>
    /// <response code="400">Invalid input</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create events (requires SeniorModerator+)</response>
    [HttpPost(Name = nameof(CreateGlobalChatEvent))]
    [RequireRole(UserRole.SeniorModerator)]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> CreateGlobalChatEvent([FromBody] CreateGlobalChatEventInput input, CancellationToken ct)
    {
        var result = await _apiService.Create(input, ct);
        return CreatedAtRoute(nameof(GetGlobalChatEvent), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Update a chat event
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="input">Updated event data</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Updated event</response>
    /// <response code="400">Invalid input</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer of this event</response>
    /// <response code="404">Event not found</response>
    [HttpPatch("{id}", Name = nameof(UpdateGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> UpdateGlobalChatEvent(Guid id, [FromBody] UpdateGlobalChatEventInput input, CancellationToken ct) =>
        Ok(await _apiService.Update(id, input, ct));

    /// <summary>
    /// Delete a chat event
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Event deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer of this event</response>
    /// <response code="404">Event not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteGlobalChatEvent(Guid id, CancellationToken ct)
    {
        await _apiService.Delete(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Start a chat event (transition from Scheduled to Live)
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Started event</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer of this event</response>
    /// <response code="409">Event is not in Scheduled status or another event is already live</response>
    /// <response code="404">Event not found</response>
    [HttpPost("{id}/start", Name = nameof(StartGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> StartGlobalChatEvent(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Start(id, ct));

    /// <summary>
    /// End a chat event (transition from Live to Ended)
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Ended event</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer of this event</response>
    /// <response code="409">Event is not in Live status</response>
    /// <response code="404">Event not found</response>
    [HttpPost("{id}/end", Name = nameof(EndGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> EndGlobalChatEvent(Guid id, CancellationToken ct) =>
        Ok(await _apiService.End(id, ct));

    /// <summary>
    /// Join an open chat event
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Event with updated participants</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Event is closed (not open for self-join)</response>
    /// <response code="404">Event not found</response>
    [HttpPost("{id}/join", Name = nameof(JoinGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> JoinGlobalChatEvent(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Join(id, ct));

    /// <summary>
    /// Leave a chat event
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Event with updated participants</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a participant</response>
    /// <response code="404">Event not found</response>
    [HttpPost("{id}/leave", Name = nameof(LeaveGlobalChatEvent))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> LeaveGlobalChatEvent(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Leave(id, ct));

    /// <summary>
    /// Get participants of a chat event
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Event with participants</response>
    /// <response code="404">Event not found</response>
    [HttpGet("{id}/participants", Name = nameof(GetGlobalChatEventParticipants))]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetGlobalChatEventParticipants(Guid id, CancellationToken ct) =>
        Ok(await _apiService.Get(id, ct));

    /// <summary>
    /// Add a participant to a closed event (organizer only)
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="input">Participant data</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Event with updated participants</response>
    /// <response code="400">Invalid input</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer or event is open</response>
    /// <response code="404">Event or user not found</response>
    [HttpPost("{id}/participants", Name = nameof(AddGlobalChatEventParticipant))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<GlobalChatEvent>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> AddGlobalChatEventParticipant(Guid id, [FromBody] AddParticipantInput input, CancellationToken ct) =>
        Ok(await _apiService.AddParticipant(id, input, ct));

    /// <summary>
    /// Remove a participant from a chat event (organizer only)
    /// </summary>
    /// <param name="id">Event identifier</param>
    /// <param name="login">User login to remove</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Participant removed</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not an organizer</response>
    /// <response code="404">Event or user not found</response>
    [HttpDelete("{id}/participants/{login}", Name = nameof(RemoveGlobalChatEventParticipant))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> RemoveGlobalChatEventParticipant(Guid id, string login, CancellationToken ct)
    {
        await _apiService.RemoveParticipant(id, login, ct);
        return NoContent();
    }
}
