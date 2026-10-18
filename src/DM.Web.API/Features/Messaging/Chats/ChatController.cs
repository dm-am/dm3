using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// Chat management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for user chats (group conversations).
/// Chats support multiple participants, typing indicators, and read receipts.
/// </remarks>
[ApiController]
[Route("v1/chats")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Chats")]
public class ChatController : ControllerBase
{
    private readonly IMessagingApiService _apiService;

    /// <summary>
    /// Creates a new instance of ChatController
    /// </summary>
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChats([FromQuery] PagingQuery q)
    {
        var (chats, paging) = await _apiService.GetChatsAsync(q);
        return Ok(new ListEnvelope<Chat>(chats, paging));
    }

    /// <summary>
    /// Get number of unread messages of current user
    /// </summary>
    /// <remarks>
    /// The total across every conversation, for the header badge. The chat list
    /// answers the same question only for the page it returns, so a reader whose
    /// unread conversations sit below the first page was shown a number that left
    /// them out.
    /// </remarks>
    /// <response code="200">Unread count</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("unread", Name = nameof(GetUnreadMessagesCount))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<ChatUnreadCount>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadMessagesCount() =>
        Ok(await _apiService.GetUnreadCountAsync());

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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrCreateDirectChat(string username) =>
        Ok(await _apiService.GetDirectChatAsync(username));

    /// <summary>
    /// Get chat of current user (by id)
    /// </summary>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <response code="200">Chat details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat not found</response>
    [HttpGet("{id}", Name = nameof(GetChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChat(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return Ok(await _apiService.GetChatAsync(guid));
        return Ok(await _apiService.GetChatByPublicIdAsync(id));
    }

    /// <summary>
    /// Mark all messages in chat as read
    /// </summary>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <response code="204">Messages marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Chat not found</response>
    [HttpDelete("{id}/messages/unread", Name = nameof(MarkChatAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkChatAsRead(string id)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        await _apiService.MarkAsReadAsync(chatId);
        return NoContent();
    }

    /// <summary>
    /// Create a new group chat
    /// </summary>
    /// <param name="createChat">Chat data</param>
    /// <response code="201">Chat created</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">A participant has blocked the author</response>
    [HttpPost(Name = nameof(CreateChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateChat([FromBody] CreateChat createChat)
    {
        var result = await _apiService.CreateChatAsync(createChat);
        return CreatedAtRoute(nameof(GetChat), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing chat (title and/or participants)
    /// </summary>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <param name="updateChat">Update data</param>
    /// <response code="200">Chat updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a participant, or a participant being added has blocked the author</response>
    /// <response code="404">Chat not found</response>
    [HttpPatch("{id}", Name = nameof(UpdateChat))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChat(string id, [FromBody] UpdateChat updateChat)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        return Ok(await _apiService.UpdateChatAsync(chatId, updateChat));
    }

    /// <summary>
    /// Leave a group chat
    /// </summary>
    /// <remarks>
    /// The chat goes out of the caller's list and reads, the other participants
    /// keep it with every message. A direct chat is not left; the page offers the
    /// action by viewer.canLeave of the chat.
    /// </remarks>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <response code="204">The caller left the chat</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">The chat is not a group chat</response>
    /// <response code="404">Chat not found, or the caller is not its participant</response>
    [HttpPost("{id}/leave", Name = nameof(LeaveChat))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveChat(string id)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        await _apiService.LeaveChatAsync(chatId);
        return NoContent();
    }

    /// <summary>
    /// Clear the history of a chat for the caller
    /// </summary>
    /// <remarks>
    /// Nothing is deleted: the messages written up to this moment are no longer
    /// given to the caller, in the messages of the chat, its last message, a
    /// message read by its id or the search. The other participants keep them.
    /// The page offers the action by viewer.canClearHistory of the chat.
    /// </remarks>
    /// <param name="id">Chat public ID (5 letters) or GUID</param>
    /// <response code="204">History cleared for the caller</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">The chat is not a direct or group chat</response>
    /// <response code="404">Chat not found, or the caller is not its participant</response>
    [HttpPost("{id}/clear-history", Name = nameof(ClearChatHistory))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearChatHistory(string id)
    {
        var chatId = await _apiService.ResolveChatIdAsync(id);
        await _apiService.ClearChatHistoryAsync(chatId);
        return NoContent();
    }

    // There is no "can a chat be started" address. The one that answered it had a
    // rule of its own - refused if either person blocked the other, never asked
    // the addressee's "Блокировать личные сообщения" - so it contradicted what
    // sending then did, and it told the asker whether the other person had put
    // them on a list at all. The question is answered where the page offers the
    // write, by the code that refuses it (MessageService): viewer.canWrite of a
    // chat read here, viewer.canMessage of a profile. Both say what the refusal
    // of the message says, and only that.
}
