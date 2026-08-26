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
/// Global chat is a site-wide chat room. Reading it needs no account — the page
/// is part of what a visitor sees; writing to it does, and a ban on public speech
/// closes that half.
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
        // Points at the route that can actually serve the line it names. The
        // Location header used to name the private-message route, which answers
        // 404 for a global chat message.
        return CreatedAtRoute(nameof(GetGlobalChatMessage), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get one global chat message
    /// </summary>
    /// <remarks>
    /// Open to whoever the global chat is open to, which is everyone: reading it
    /// needs no account, and one line of it is part of the same page. Sending
    /// `X-Dm-Audience: author_edit` returns the BBCode source instead of the
    /// render, and only to the author of the line — the audience is downgraded
    /// for every other viewer.
    /// </remarks>
    /// <param name="id">Message identifier</param>
    /// <response code="200">Message retrieved successfully</response>
    /// <response code="404">Message not found in the global chat</response>
    [HttpGet("messages/{id:guid}", Name = nameof(GetGlobalChatMessage))]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobalChatMessage(Guid id) =>
        Ok(await _apiService.GetGlobalChatMessageAsync(id));

    /// <summary>
    /// Get quotation source of a global chat message
    /// </summary>
    /// <remarks>
    /// Returns the BBCode a reader's composer is filled with when they quote the
    /// line, wrapped in a quotation attributed to its author. Open to whoever the
    /// global chat is open to, which is everyone, for the same reason reading one
    /// line of it is: the quotation is composed from that very read.
    ///
    /// The quotation of a private message stays on `/v1/messages/{id}/quote`,
    /// which reads it as a participant of its chat. That read has no answer for a
    /// line of the global chat, and this route is its neighbour rather than a
    /// move of it.
    /// </remarks>
    /// <param name="id">Message identifier</param>
    /// <response code="200">Returns the quotation source</response>
    /// <response code="400">Message text could not be parsed</response>
    /// <response code="404">Message not found in the global chat</response>
    [HttpGet("messages/{id:guid}/quote", Name = nameof(GetGlobalChatMessageQuote))]
    [ProducesResponseType(typeof(Envelope<QuoteSource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobalChatMessageQuote(Guid id) =>
        Ok(await _apiService.GetGlobalChatMessageQuoteAsync(id));

    /// <summary>
    /// Edit a global chat message
    /// </summary>
    /// <remarks>
    /// The author rewrites their own line while the editing window is open;
    /// moderation rewrites any line at any time. A ban on public speech closes
    /// the author's half of it — the global chat is public speech, so editing it
    /// is too.
    /// </remarks>
    /// <param name="id">Message identifier</param>
    /// <param name="input">Updated message content</param>
    /// <response code="200">Message updated successfully</response>
    /// <response code="400">Some message parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to edit this message</response>
    /// <response code="404">Message not found in the global chat</response>
    [HttpPatch("messages/{id:guid}", Name = nameof(PatchGlobalChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchGlobalChatMessage(Guid id, [FromBody] UpdateMessageInput input)
    {
        var message = new Message { Text = new GlobalChatBbText { Value = input.Text } };
        return Ok(await _apiService.UpdateGlobalChatMessageAsync(id, message));
    }

    /// <summary>
    /// Delete a global chat message
    /// </summary>
    /// <remarks>
    /// The author takes their own line down while the editing window is open;
    /// somebody else's line comes down by moderation, Moderator and above — the
    /// same rule a private message is deleted by.
    /// </remarks>
    /// <param name="id">Message identifier</param>
    /// <response code="204">Message deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this message</response>
    /// <response code="404">Message not found in the global chat</response>
    [HttpDelete("messages/{id:guid}", Name = nameof(DeleteGlobalChatMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGlobalChatMessage(Guid id)
    {
        await _apiService.DeleteGlobalChatMessageAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Like a global chat message
    /// </summary>
    /// <remarks>
    /// Anybody signed in, on anybody's line but their own.
    /// </remarks>
    /// <param name="id">Message identifier</param>
    /// <response code="200">Like added successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like this message</response>
    /// <response code="404">Message not found in the global chat</response>
    /// <response code="409">User already liked this message</response>
    [HttpPost("messages/{id:guid}/likes", Name = nameof(PostGlobalChatMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostGlobalChatMessageLike(Guid id) =>
        Ok(await _apiService.LikeGlobalChatMessageAsync(id));

    /// <summary>
    /// Take back a like from a global chat message
    /// </summary>
    /// <param name="id">Message identifier</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like this message</response>
    /// <response code="404">Message not found in the global chat</response>
    /// <response code="409">User never liked this message</response>
    [HttpDelete("messages/{id:guid}/likes", Name = nameof(DeleteGlobalChatMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteGlobalChatMessageLike(Guid id)
    {
        await _apiService.UnlikeGlobalChatMessageAsync(id);
        return NoContent();
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
