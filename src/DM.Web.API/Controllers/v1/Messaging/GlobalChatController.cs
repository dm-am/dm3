using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Messaging;
using DM.Web.API.Services.Community;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Messaging;

/// <inheritdoc />
[ApiController]
[Route("v1/globalchat")]
[ApiExplorerSettings(GroupName = "Messaging")]
public class GlobalChatController : ControllerBase
{
    private readonly IChatApiService _chatApiService;

    /// <inheritdoc />
    public GlobalChatController(
        IChatApiService chatApiService)
    {
        _chatApiService = chatApiService;
    }

    /// <summary>
    /// Get chat messages
    /// </summary>
    /// <param name="q">Paging query</param>
    /// <response code="200"></response>
    [HttpGet("messages", Name = nameof(GetChatMessages))]
    [ProducesResponseType(typeof(ListEnvelope<ChatMessage>), 200)]
    public async Task<IActionResult> GetChatMessages([FromQuery] PagingQuery q) =>
        Ok(await _chatApiService.GetMessages(q));

    /// <summary>
    /// Send chat message
    /// </summary>
    /// <param name="message">Message</param>
    /// <response code="201"></response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost("messages", Name = nameof(PostChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> PostChatMessage([FromBody] ChatMessage message)
    {
        var result = await _chatApiService.CreateMessage(message);
        return CreatedAtRoute(nameof(GetChatMessage), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get chat message by id
    /// </summary>
    /// <param name="id">Message id</param>
    /// <response code="200"></response>
    /// <response code="410">Message not found</response>
    [HttpGet("messages/{id}", Name = nameof(GetChatMessage))]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetChatMessage(Guid id) =>
        Ok(await _chatApiService.GetMessage(id));

    /// <summary>
    /// Update chat message
    /// </summary>
    /// <param name="id">Message id</param>
    /// <param name="message">Message data</param>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot edit this message</response>
    /// <response code="410">Message not found</response>
    [HttpPatch("messages/{id}", Name = nameof(PatchChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchChatMessage(Guid id, [FromBody] ChatMessage message) =>
        Ok(await _chatApiService.UpdateMessage(id, message));

    /// <summary>
    /// Delete chat message
    /// </summary>
    /// <param name="id">Message id</param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot delete this message</response>
    /// <response code="410">Message not found</response>
    [HttpDelete("messages/{id}", Name = nameof(DeleteChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteChatMessage(Guid id)
    {
        await _chatApiService.DeleteMessage(id);
        return NoContent();
    }

    /// <summary>
    /// Like chat message
    /// </summary>
    /// <param name="id">Message id</param>
    /// <response code="201"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this message</response>
    /// <response code="409">User already liked this message</response>
    /// <response code="410">Message not found</response>
    [HttpPost("messages/{id}/likes", Name = nameof(LikeChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> LikeChatMessage(Guid id) =>
        Created(string.Empty, await _chatApiService.LikeMessage(id));

    /// <summary>
    /// Unlike chat message
    /// </summary>
    /// <param name="id">Message id</param>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot unlike this message</response>
    /// <response code="409">User never liked this message</response>
    /// <response code="410">Message not found</response>
    [HttpDelete("messages/{id}/likes", Name = nameof(UnlikeChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> UnlikeChatMessage(Guid id) =>
        Ok(await _chatApiService.UnlikeMessage(id));

    /// <summary>
    /// Get chat messages for a specific date (logs)
    /// </summary>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    /// <response code="200"></response>
    [HttpGet("logs/{date}", Name = nameof(GetChatMessagesByDate))]
    [ProducesResponseType(typeof(ListEnvelope<ChatMessage>), 200)]
    public async Task<IActionResult> GetChatMessagesByDate(DateOnly date) =>
        Ok(await _chatApiService.GetMessagesByDate(date));

    /// <summary>
    /// Get first chat message on or after a specific date
    /// </summary>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    /// <response code="200">Message found</response>
    /// <response code="404">No messages on or after this date</response>
    [HttpGet("logs/{date}/first", Name = nameof(GetFirstChatMessageOnOrAfterDate))]
    [ProducesResponseType(typeof(Envelope<ChatMessage>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFirstChatMessageOnOrAfterDate(DateOnly date)
    {
        var result = await _chatApiService.GetFirstMessageOnOrAfterDate(date);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Get chat messages before (older than) a specific message
    /// </summary>
    /// <param name="id">Reference message ID</param>
    /// <param name="count">Number of messages to fetch (default 50)</param>
    /// <response code="200"></response>
    [HttpGet("messages/{id}/before", Name = nameof(GetChatMessagesBefore))]
    [ProducesResponseType(typeof(ListEnvelope<ChatMessage>), 200)]
    public async Task<IActionResult> GetChatMessagesBefore(Guid id, [FromQuery] int count = 50) =>
        Ok(await _chatApiService.GetMessagesBefore(id, count));

    /// <summary>
    /// Get chat messages after (newer than) a specific message
    /// </summary>
    /// <param name="id">Reference message ID</param>
    /// <param name="count">Number of messages to fetch (default 50)</param>
    /// <response code="200"></response>
    [HttpGet("messages/{id}/after", Name = nameof(GetChatMessagesAfter))]
    [ProducesResponseType(typeof(ListEnvelope<ChatMessage>), 200)]
    public async Task<IActionResult> GetChatMessagesAfter(Guid id, [FromQuery] int count = 50) =>
        Ok(await _chatApiService.GetMessagesAfter(id, count));

    /// <summary>
    /// Get chat messages around a specific message
    /// </summary>
    /// <param name="id">Reference message ID</param>
    /// <param name="count">Total number of messages to fetch (default 50)</param>
    /// <response code="200"></response>
    [HttpGet("messages/{id}/around", Name = nameof(GetChatMessagesAround))]
    [ProducesResponseType(typeof(ListEnvelope<ChatMessage>), 200)]
    public async Task<IActionResult> GetChatMessagesAround(Guid id, [FromQuery] int count = 50) =>
        Ok(await _chatApiService.GetMessagesAround(id, count));
}
