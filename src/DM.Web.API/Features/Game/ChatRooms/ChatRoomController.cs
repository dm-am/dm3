using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Messaging.Messages;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Chat room management endpoints
/// </summary>
/// <remarks>
/// Chat rooms are special rooms in games used for out-of-character player communication.
/// They use message-based (cursor pagination) instead of post-based content.
/// </remarks>
[ApiController]
[Route("v1/chat-rooms")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Chat Rooms")]
public class ChatRoomController : ControllerBase
{
    private readonly IChatRoomApiService _apiService;
    private readonly IGameApiService _gameApiService;

    /// <summary>
    /// Creates a new instance of ChatRoomController
    /// </summary>
    public ChatRoomController(IChatRoomApiService apiService, IGameApiService gameApiService)
    {
        _apiService = apiService;
        _gameApiService = gameApiService;
    }

    /// <summary>
    /// Get chat rooms in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the list of chat rooms</response>
    /// <response code="404">Game not found</response>
    [HttpGet("~/v1/games/{id}/chat-rooms", Name = nameof(GetChatRooms))]
    [ProducesResponseType(typeof(ListEnvelope<ChatRoom>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatRooms(string id) =>
        Ok(await _apiService.GetChatRoomsAsync(await _gameApiService.ResolveId(id)));

    /// <summary>
    /// Create new chat room in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="input">Chat room creation data</param>
    /// <response code="201">Chat room created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create chat rooms in this game</response>
    /// <response code="404">Game not found</response>
    [HttpPost("~/v1/games/{id}/chat-rooms", Name = nameof(PostChatRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatRoom>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostChatRoom(string id, [FromBody] CreateChatRoom input)
    {
        var gameId = await _gameApiService.ResolveId(id);
        var result = await _apiService.CreateChatRoomAsync(gameId, input);
        return CreatedAtRoute(nameof(GetChatRoom),
            new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <response code="200">Returns the chat room details</response>
    /// <response code="404">Chat room not found</response>
    [HttpGet("{id}", Name = nameof(GetChatRoom))]
    [ProducesResponseType(typeof(Envelope<ChatRoom>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatRoom(Guid id) =>
        Ok(await _apiService.GetChatRoomAsync(id));

    /// <summary>
    /// Update chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <param name="input">Updated chat room data</param>
    /// <response code="200">Returns the updated chat room</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to update this chat room</response>
    /// <response code="404">Chat room not found</response>
    [HttpPatch("{id}", Name = nameof(PatchChatRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatRoom>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchChatRoom(Guid id, [FromBody] UpdateChatRoom input) =>
        Ok(await _apiService.UpdateChatRoomAsync(id, input));

    /// <summary>
    /// Delete chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <response code="204">Chat room deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to delete this chat room</response>
    /// <response code="404">Chat room not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteChatRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChatRoom(Guid id)
    {
        await _apiService.DeleteChatRoomAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Get messages in chat room (cursor-based pagination)
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <param name="cursor">Pagination cursor</param>
    /// <param name="limit">Maximum number of messages (1-100, default 50)</param>
    /// <response code="200">Returns messages with pagination cursor</response>
    /// <response code="400">Limit is outside the 1-100 range</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to view messages in this chat room</response>
    /// <response code="404">Chat room not found</response>
    [HttpGet("{id}/messages", Name = nameof(GetChatRoomMessages))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(CursorEnvelope<Message>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatRoomMessages(
        Guid id,
        [FromQuery] string? cursor = null,
        // Rejected, not clipped: a caller who asked for a thousand has to learn
        // that a thousand is not on offer instead of taking a hundred for the
        // whole set. The other three cursor lists — chats, global chat and
        // message search — say the same thing now; until they did, this comment
        // described a rule only one endpoint followed, while they clamped in
        // silence two layers down.
        [FromQuery][Range(1, 100, ErrorMessage = "Размер страницы должен быть от 1 до 100")] int limit = 50) =>
        Ok(await _apiService.GetMessagesAsync(id, cursor, limit));

    /// <summary>
    /// Send message to chat room
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <param name="input">Message content</param>
    /// <response code="201">Message created successfully</response>
    /// <response code="400">Invalid message data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to send messages to this chat room</response>
    /// <response code="404">Chat room not found</response>
    [HttpPost("{id}/messages", Name = nameof(PostChatRoomMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostChatRoomMessage(
        Guid id,
        [FromBody] CreateMessageInput input)
    {
        var result = await _apiService.CreateMessageAsync(id, input);
        return CreatedAtRoute(nameof(MessageController.GetMessage), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Mark all messages in chat room as read
    /// </summary>
    /// <param name="id">Chat room identifier</param>
    /// <response code="204">Messages marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat room not found</response>
    [HttpDelete("{id}/messages/unread", Name = nameof(MarkChatRoomMessagesAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkChatRoomMessagesAsRead(Guid id)
    {
        await _apiService.MarkAsReadAsync(id);
        return NoContent();
    }
}
