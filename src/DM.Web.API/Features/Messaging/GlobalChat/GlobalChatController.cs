using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Messaging.Messages;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Messaging.GlobalChat;

/// <summary>
/// Global chat message endpoints
/// </summary>
/// <remarks>
/// Provides access to global chat messages for all authenticated users.
/// Global chat is a site-wide chat room visible to everyone.
/// </remarks>
[ApiController]
[Route("v1/global-chat")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Global Chat")]
public class GlobalChatController : ControllerBase
{
    private readonly IMessagingApiService _apiService;

    /// <summary>
    /// Creates a new instance of GlobalChatController
    /// </summary>
    public GlobalChatController(IMessagingApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get global chat messages with cursor-based pagination
    /// </summary>
    /// <remarks>
    /// Returns messages from the global chat.
    /// Supports cursor-based pagination for infinite scroll.
    /// </remarks>
    /// <param name="cursor">Opaque cursor for pagination</param>
    /// <param name="aroundMessageId">Get messages around this message</param>
    /// <param name="nearTimestampUtc">Get messages near this UTC timestamp</param>
    /// <param name="limit">Maximum number of messages (1-100, default 50)</param>
    /// <response code="200">Messages with cursor pagination</response>
    [HttpGet("messages", Name = nameof(GetGlobalChatMessages))]
    [ProducesResponseType(typeof(CursorEnvelope<Message>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalChatMessages(
        [FromQuery] string? cursor = null,
        [FromQuery] Guid? aroundMessageId = null,
        [FromQuery] DateTimeOffset? nearTimestampUtc = null,
        [FromQuery][Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 до 100")] int limit = 50) =>
        Ok(await _apiService.GetGlobalChatMessagesAsync(cursor, aroundMessageId, nearTimestampUtc, limit));

    /// <summary>
    /// Send message to global chat
    /// </summary>
    /// <param name="input">Message content</param>
    /// <response code="201">Message created</response>
    /// <response code="400">Invalid input</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is banned from global chat</response>
    [HttpPost("messages", Name = nameof(PostGlobalChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostGlobalChatMessage([FromBody] CreateMessageInput input)
    {
        // Surface is GlobalChatMessage so the safe-image parser applies; the
        // response is re-projected from the domain (see MessageTextResolver),
        // this carrier only ferries the raw text into the create pipeline.
        var message = new Message { Text = new GlobalChatBbText { Value = input.Text } };
        var result = await _apiService.CreateGlobalChatMessageAsync(message);
        return CreatedAtRoute("GetMessage", new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Mark global chat as read
    /// </summary>
    /// <response code="204">Marked as read</response>
    /// <response code="401">User must be authenticated</response>
    [HttpDelete("messages/unread", Name = nameof(MarkGlobalChatAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkGlobalChatAsRead()
    {
        await _apiService.MarkGlobalChatAsReadAsync();
        return NoContent();
    }
}
