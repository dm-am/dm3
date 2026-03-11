using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Messaging.Chats;

/// <inheritdoc />
[ApiController]
[Route("v1/chats")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Chats")]
public class ChatController : ControllerBase
{
    private readonly IMessagingApiService _apiService;

    /// <inheritdoc />
    public ChatController(
        IMessagingApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of chats of current user
    /// </summary>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of chats</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetChats))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Chat>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChats([FromQuery] PagingQuery q)
    {
        var (chats, paging) = await _apiService.GetChats(q);
        return Ok(new ListEnvelope<Chat>(chats, paging));
    }

    /// <summary>
    /// Get or create 1-on-1 chat of current user with another user
    /// </summary>
    /// <remarks>
    /// This endpoint creates the chat if it doesn't exist, hence POST method.
    /// Returns existing chat if already present.
    /// </remarks>
    /// <param name="username">User's display name</param>
    /// <response code="200">Chat retrieved or created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found</response>
    [HttpPost("direct/{username}", Name = nameof(GetOrCreateDirectChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrCreateDirectChat(string username) =>
        Ok(await _apiService.GetDirectChat(username));

    /// <summary>
    /// Get chat of current user (by id)
    /// </summary>
    /// <param name="id">Chat identifier</param>
    /// <response code="200">Chat details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChat(Guid id) =>
        Ok(await _apiService.GetChat(id));

    /// <summary>
    /// Mark all messages in chat as read
    /// </summary>
    /// <response code="204">Messages marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat not found</response>
    [HttpDelete("{id:guid}/messages/unread", Name = nameof(MarkChatAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkChatAsRead(Guid id)
    {
        await _apiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Create a new group chat
    /// </summary>
    /// <param name="createChat">Chat data</param>
    /// <response code="201">Chat created</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(CreateChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateChat([FromBody] CreateChat createChat)
    {
        var result = await _apiService.CreateChat(createChat);
        return CreatedAtRoute(nameof(GetChat), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing chat (title and/or participants)
    /// </summary>
    /// <param name="id">Chat identifier</param>
    /// <param name="updateChat">Update data</param>
    /// <response code="200">Chat updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a participant</response>
    /// <response code="404">Chat not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChat(Guid id, [FromBody] UpdateChat updateChat) =>
        Ok(await _apiService.UpdateChat(id, updateChat));

    /// <summary>
    /// Check if chat can be started with another user
    /// </summary>
    /// <remarks>
    /// Checks if current user can start a chat with another user.
    /// Returns false if EITHER user has blocked the other.
    ///
    /// Use cases:
    /// - Show/hide "Send message" button in user profile
    /// - Check before opening new chat dialog
    /// - Provide clear reason for blocked messaging
    /// </remarks>
    /// <param name="username">Target username</param>
    /// <response code="200">Chat availability result</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Target user not found</response>
    /// <response code="429">Too many requests</response>
    [HttpGet("can-start/{username}", Name = nameof(CanStartChat))]
    [AuthenticationRequired]
    [EnableRateLimiting("sliding")]
    [ProducesResponseType(typeof(ChatAvailability), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CanStartChat(string username)
    {
        var result = await _apiService.CanStartChat(username);
        return Ok(result);
    }
}
