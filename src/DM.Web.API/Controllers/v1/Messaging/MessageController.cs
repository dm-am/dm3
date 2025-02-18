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
public class MessageController : ControllerBase
{
    private readonly IMessagingApiService apiService;

    /// <inheritdoc />
    public MessageController(
        IMessagingApiService apiService)
    {
        this.apiService = apiService;
    }

    /// <summary>
    /// Get list of messages in conversation
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Dialogue not found</response>
    [HttpGet("conversations/{id}/messages", Name = nameof(GetMessages))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Message>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await apiService.GetMessages(id, q));

    /// <summary>
    /// Create message in conversation
    /// </summary>
    /// <response code="201"></response>
    /// <response code="400">Some message parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create message in this conversation</response>
    /// <response code="410">Dialogue not found</response>
    [HttpPost("conversations/{id}/messages", Name = nameof(PostMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Message>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostMessage(Guid id, [FromBody] Message message)
    {
        var result = await apiService.CreateMessage(id, message);
        return CreatedAtRoute(nameof(GetMessage), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get message
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Message not found</response>
    [HttpGet("messages/{id}", Name = nameof(GetMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetMessage(Guid id) => Ok(await apiService.GetMessage(id));

    /// <summary>
    /// Update message
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Message not found</response>
    [HttpPatch("messages/{id}", Name = nameof(PatchMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Message>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    // TODO: Update message
    public Task<IActionResult> PatchMessage(Guid id) => throw new NotImplementedException();

    /// <summary>
    /// Delete message
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Message not found</response>
    [HttpDelete("messages/{id}", Name = nameof(DeleteMessage))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        await apiService.DeleteMessage(id);
        return NoContent();
    }

    /// <summary>
    /// Add new like for message
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Message not found</response>
    [HttpPost("messages/{id}/likes", Name = nameof(PostMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    // TODO: Add new like for message
    public Task<IActionResult> PostMessageLike(Guid id) => throw new NotImplementedException();

    /// <summary>
    /// Delete like from message
    /// </summary>
    /// <response code="200"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Message not found</response>
    [HttpDelete("messages/{id}/likes", Name = nameof(DeleteMessageLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    // TODO: Delete like from message
    public Task<IActionResult> DeleteMessageLike(Guid id) => throw new NotImplementedException();
}