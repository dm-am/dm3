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
[Route("v1")]
[ApiExplorerSettings(GroupName = "Messaging")]
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
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("conversations", Name = nameof(GetConversations))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Conversation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetConversations([FromQuery] PagingQuery q) =>
        Ok(await _apiService.GetConversations(q));

    /// <summary>
    /// Get 1-on-1 conversation of current user with another user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">User not found</response>
    [HttpGet("conversations/direct/{userId:guid}", Name = nameof(GetDirectConversationById))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetDirectConversationById(Guid userId) =>
        Ok(await _apiService.GetDirectConversation(userId));

    /// <summary>
    /// Get 1-on-1 conversation of current user with another user by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">User not found</response>
    [HttpGet("conversations/direct/{login}", Name = nameof(GetDirectConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetDirectConversation(string login) =>
        Ok(await _apiService.GetConversation(login));

    /// <summary>
    /// Get conversation of current user (by id)
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Dialogue not found</response>
    [HttpGet("conversations/{id}", Name = nameof(GetConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetConversation(Guid id) =>
        Ok(await _apiService.GetConversation(id));

    /// <summary>
    /// Mark all messages in conversation as read
    /// </summary>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Dialogue not found</response>
    [HttpDelete("conversations/{id}/messages/unread", Name = nameof(MarkAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
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
    [HttpPost("conversations", Name = nameof(CreateConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
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
    /// <response code="410">Conversation not found</response>
    [HttpPatch("conversations/{id}", Name = nameof(UpdateConversation))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Conversation>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> UpdateConversation(Guid id, [FromBody] UpdateConversation updateConversation) =>
        Ok(await _apiService.UpdateConversation(id, updateConversation));
}