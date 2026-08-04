using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Messaging.Messages;

/// <summary>
/// Message management endpoints
/// </summary>
/// <remarks>
/// Provides operations for individual messages within conversations and chats.
/// Supports cursor-based pagination, editing, and deletion.
/// </remarks>
[ApiController]
[Route("v1/messages")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Messages")]
public class MessageController : ControllerBase
{
    private readonly IMessagingApiService _apiService;

    /// <summary>
    /// Creates a new instance of MessageController
    /// </summary>
    public MessageController(
        IMessagingApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of messages in chat with cursor-based pagination
    /// </summary>
    /// <remarks>
    /// This endpoint supports multiple pagination modes:
    /// - **Default**: Without parameters, returns the most recent messages
    /// - **Cursor**: Use `cursor` parameter from previous response's `paging.nextCursor` or `paging.prevCursor`
    /// - **Around message**: Use `aroundMessageId` to get messages centered around a specific message
    /// - **Near timestamp**: Use `nearTimestampUtc` to get messages near a specific time
    ///
    /// The `limit` parameter controls how many messages to return (max 100, default 50).
    /// </remarks>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <param name="cursor">Opaque cursor for pagination (from previous response)</param>
    /// <param name="aroundMessageId">Get messages around this message</param>
    /// <param name="nearTimestampUtc">Get messages near this UTC timestamp (ISO 8601 format)</param>
    /// <param name="limit">Maximum number of messages to return (1-100, default 50)</param>
    /// <response code="200">Messages with cursor pagination info</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat not found</response>
    [HttpGet("~/v1/chats/{id}/messages", Name = nameof(GetMessages))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(CursorEnvelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        string id,
        [FromQuery] string? cursor = null,
        [FromQuery] Guid? aroundMessageId = null,
        [FromQuery] DateTimeOffset? nearTimestampUtc = null,
        [FromQuery] int limit = 50)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        return Ok(await _apiService.GetMessagesWithCursorAsync(chatId, cursor, aroundMessageId, nearTimestampUtc, limit));
    }

    /// <summary>
    /// Create message in chat
    /// </summary>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <param name="input">Message content</param>
    /// <response code="201">Message created successfully</response>
    /// <response code="400">Some message parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create message in this chat</response>
    /// <response code="404">Chat not found</response>
    [HttpPost("~/v1/chats/{id}/messages", Name = nameof(PostMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostMessage(string id, [FromBody] CreateMessageInput input)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        var message = new Message { Text = new CommonBbText { Value = input.Text } };
        var result = await _apiService.CreateMessageAsync(chatId, message);
        return CreatedAtRoute(nameof(GetMessage), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <response code="200">Message retrieved successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Message not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessage(Guid id) => Ok(await _apiService.GetMessageAsync(id));

    /// <summary>
    /// Update message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <param name="input">Updated message content</param>
    /// <response code="200">Message updated successfully</response>
    /// <response code="400">Some message parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to edit this message</response>
    /// <response code="404">Message not found</response>
    [HttpPatch("{id:guid}", Name = nameof(PatchMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchMessage(Guid id, [FromBody] UpdateMessageInput input)
    {
        var message = new Message { Text = new CommonBbText { Value = input.Text } };
        return Ok(await _apiService.UpdateMessageAsync(id, message));
    }

    /// <summary>
    /// Delete message
    /// </summary>
    /// <response code="204">Message deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this message</response>
    /// <response code="404">Message not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        await _apiService.DeleteMessageAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Add new like for message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <response code="200">Like added successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="409">User already liked this message</response>
    /// <response code="404">Message not found</response>
    [HttpPost("{id:guid}/likes", Name = nameof(PostMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostMessageLike(Guid id) =>
        Ok(await _apiService.LikeMessageAsync(id));

    /// <summary>
    /// Delete like from message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="409">User never liked this message</response>
    /// <response code="404">Message not found</response>
    [HttpDelete("{id:guid}/likes", Name = nameof(DeleteMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMessageLike(Guid id)
    {
        await _apiService.UnlikeMessageAsync(id);
        return NoContent();
    }
}
