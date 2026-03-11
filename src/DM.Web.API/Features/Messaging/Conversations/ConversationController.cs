using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Messaging.Conversations;

/// <inheritdoc />
[ApiController]
[Route("v1/conversations")]
[ApiExplorerSettings(GroupName = "Messaging")]
[Tags("Conversations")]
public class ConversationController : ControllerBase
{
    private readonly IMessagingApiService _apiService;

    /// <inheritdoc />
    public ConversationController(
        IMessagingApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of conversations of current user
    /// </summary>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">List of conversations</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetConversations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Conversation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations([FromQuery] PagingQuery q) =>
        Ok(await _apiService.GetConversations(q));

    /// <summary>
    /// Get or create 1-on-1 conversation of current user with another user
    /// </summary>
    /// <remarks>
    /// This endpoint creates the conversation if it doesn't exist, hence POST method.
    /// Returns existing conversation if already present.
    /// </remarks>
    /// <param name="login">User login</param>
    /// <response code="200">Conversation retrieved or created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found</response>
    [HttpPost("direct/{login}", Name = nameof(GetOrCreateDirectConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrCreateDirectConversation(string login) =>
        Ok(await _apiService.GetDirectConversation(login));

    /// <summary>
    /// Get conversation of current user (by id)
    /// </summary>
    /// <param name="id">Conversation identifier</param>
    /// <response code="200">Conversation details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Conversation not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(Guid id) =>
        Ok(await _apiService.GetConversation(id));

    /// <summary>
    /// Mark all messages in conversation as read
    /// </summary>
    /// <response code="204">Messages marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Conversation not found</response>
    [HttpDelete("{id:guid}/messages/unread", Name = nameof(MarkAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _apiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Create a new group conversation
    /// </summary>
    /// <param name="createConversation">Conversation data</param>
    /// <response code="201">Conversation created</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(CreateConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversation createConversation)
    {
        var result = await _apiService.CreateConversation(createConversation);
        return CreatedAtRoute(nameof(GetConversation), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Update an existing conversation (title and/or participants)
    /// </summary>
    /// <param name="id">Conversation identifier</param>
    /// <param name="updateConversation">Update data</param>
    /// <response code="200">Conversation updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a participant</response>
    /// <response code="404">Conversation not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConversation(Guid id, [FromBody] UpdateConversation updateConversation) =>
        Ok(await _apiService.UpdateConversation(id, updateConversation));
}
