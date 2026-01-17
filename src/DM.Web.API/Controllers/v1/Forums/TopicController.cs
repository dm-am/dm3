using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forums;

/// <inheritdoc />
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Forum")]
public class TopicController : ControllerBase
{
    private readonly ITopicApiService _topicApiService;
    private readonly ILikeApiService _likeApiService;
    private readonly ICommentApiService _commentApiService;

    /// <inheritdoc />
    public TopicController(
        ITopicApiService topicApiService,
        ILikeApiService likeApiService,
        ICommentApiService commentApiService)
    {
        _topicApiService = topicApiService;
        _likeApiService = likeApiService;
        _commentApiService = commentApiService;
    }

    /// <summary>
    /// Get list of topics on board
    /// </summary>
    /// <param name="id">Forum id</param>
    /// <param name="q">Query</param>
    /// <response code="200"></response>
    /// <response code="400">Some properties of the passed search parameters were invalid</response>
    /// <response code="410">Forum not found</response>
    [HttpGet("boards/{id}/topics", Name = nameof(GetBoardTopics))]
    [ProducesResponseType(typeof(ListEnvelope<Topic>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetBoardTopics(string id, [FromQuery] TopicsQuery q) =>
        Ok(await _topicApiService.Get(id, q));

    /// <summary>
    /// Create new topic on board
    /// </summary>
    /// <param name="id">Forum id</param>
    /// <param name="topic">New topic</param>
    /// <response code="201"></response>
    /// <response code="400">Some of the passed topic properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create topics in this forum</response>
    /// <response code="410">Forum not found</response>
    [HttpPost("boards/{id}/topics", Name = nameof(PostBoardTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostBoardTopic(string id, [FromBody] Topic topic)
    {
        var result = await _topicApiService.Create(id, topic);
        return CreatedAtRoute(nameof(TopicController.GetTopic), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get topic
    /// </summary>
    /// <param name="id"></param>
    /// <response code="200"></response>
    /// <response code="410">Topic not found</response>
    [HttpGet("topics/{id}", Name = nameof(GetTopic))]
    [ProducesResponseType(typeof(Envelope<Topic>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetTopic(Guid id) => Ok(await _topicApiService.Get(id));

    /// <summary>
    /// Update topic
    /// </summary>
    /// <param name="id"></param>
    /// <param name="topic">Topic</param>
    /// <response code="200"></response>
    /// <response code="400">Some of topic changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpPatch("topics/{id}", Name = nameof(PatchTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchTopic(Guid id, [FromBody] Topic topic) =>
        Ok(await _topicApiService.Update(id, topic));

    /// <summary>
    /// Delete topic
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the topic</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteTopic(Guid id)
    {
        await _topicApiService.Delete(id);
        return NoContent();
    }


    /// <summary>
    /// Add new like to topic
    /// </summary>
    /// <param name="id"></param>
    /// <response code="201"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like the topic</response>
    /// <response code="409">User already liked this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpPost("topics/{id}/likes", Name = nameof(PostTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostTopicLike(Guid id) =>
        CreatedAtRoute(nameof(GetTopic), new {id}, await _likeApiService.LikeTopic(id));

    /// <summary>
    /// Delete like from topic
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove like from this topic</response>
    /// <response code="409">User has no like for this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("topics/{id}/likes", Name = nameof(DeleteTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteTopicLike(Guid id)
    {
        await _likeApiService.DislikeTopic(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all comments in topic as read
    /// </summary>
    /// <param name="id">Topic id</param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("topics/{id}/comments/unread", Name = nameof(ReadTopicComments))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> ReadTopicComments(Guid id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }
}