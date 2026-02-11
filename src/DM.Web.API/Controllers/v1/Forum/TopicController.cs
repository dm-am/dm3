using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forum;

/// <summary>
/// Topic management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for forum topics within boards.
/// Topics contain the main discussion content and associated comments.
///
/// ## Available Operations
/// - List topics in a board (with pagination)
/// - Create, read, update, delete topics
/// - Like/unlike topics
/// - Mark topic comments as read
///
/// ## Access Control
/// - Viewing: Controlled by board's ViewPolicy
/// - Creating: Controlled by board's CreateTopicPolicy
/// - Editing: Author or moderators
/// - Deleting: Author or moderators
/// - Liking: Authenticated users (cannot like own topics)
/// </remarks>
[ApiController]
[Route("v1/topics")]
[ApiExplorerSettings(GroupName = "Forum")]
[Tags("Forum Topics")]
public class TopicController : ControllerBase
{
    private readonly ITopicApiService _topicApiService;
    private readonly ILikeApiService _likeApiService;
    private readonly ICommentApiService _commentApiService;

    /// <summary>
    /// Creates a new instance of TopicController
    /// </summary>
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
    /// <remarks>
    /// Returns paginated list of topics in the specified board.
    /// Topics are sorted by last activity date (newest first) by default.
    /// Includes unread comment counts for authenticated users.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <param name="q">Pagination and filtering parameters</param>
    /// <response code="200">Paginated list of topics</response>
    /// <response code="400">Invalid query parameters</response>
    /// <response code="410">Board not found</response>
    [HttpGet("~/v1/boards/{id}/topics", Name = nameof(GetBoardTopics))]
    [ProducesResponseType(typeof(ListEnvelope<Topic>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetBoardTopics(string id, [FromQuery] TopicsQuery q) =>
        Ok(await _topicApiService.Get(id, q));

    /// <summary>
    /// Create new topic on board
    /// </summary>
    /// <remarks>
    /// Creates a new discussion topic in the specified board.
    /// The topic title and first comment text are required.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <param name="request">Topic data including title and content</param>
    /// <response code="201">Topic created successfully</response>
    /// <response code="400">Invalid topic data (title too short, empty content, etc.)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create topics in this board</response>
    /// <response code="410">Board not found</response>
    [HttpPost("~/v1/boards/{id}/topics", Name = nameof(PostBoardTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostBoardTopic(string id, [FromBody] CreateTopicRequest request)
    {
        var result = await _topicApiService.Create(id, request);
        return CreatedAtRoute(nameof(TopicController.GetTopic), new { id = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get topic details
    /// </summary>
    /// <remarks>
    /// Returns detailed information about a specific topic including:
    /// - Topic title and content
    /// - Author information
    /// - Comment count and likes
    /// - Creation and last update timestamps
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <response code="200">Topic details</response>
    /// <response code="410">Topic not found</response>
    [HttpGet("{id}", Name = nameof(GetTopic))]
    [ProducesResponseType(typeof(Envelope<Topic>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetTopic(Guid id) => Ok(await _topicApiService.Get(id));

    /// <summary>
    /// Get topic discussion with comments and permission flags
    /// </summary>
    /// <remarks>
    /// Returns a unified discussion view with:
    /// - Paginated comments
    /// - Permission flags (CanEdit, CanDelete, CanLike) for each comment
    /// - Total likes count across all comments
    /// - CanComment flag for the current user
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <param name="q">Paging parameters</param>
    /// <response code="200">Discussion with comments and metadata</response>
    /// <response code="410">Topic not found</response>
    [HttpGet("{id}/discussion", Name = nameof(GetTopicDiscussion))]
    [ProducesResponseType(typeof(DiscussionResponse), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetTopicDiscussion(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await _commentApiService.GetDiscussion(id, q));

    /// <summary>
    /// Update topic
    /// </summary>
    /// <remarks>
    /// Updates topic properties. Only provided fields will be updated.
    ///
    /// Available fields:
    /// - Title: Topic title (author or moderator)
    /// - Text: First comment content (author or moderator)
    /// - Closed: Lock/unlock topic (moderator only)
    /// - Attached: Pin/unpin topic (moderator only)
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <param name="topic">Fields to update</param>
    /// <response code="200">Updated topic</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot modify this topic or specific fields</response>
    /// <response code="410">Topic not found</response>
    [HttpPatch("{id}", Name = nameof(PatchTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchTopic(Guid id, [FromBody] Topic topic) =>
        Ok(await _topicApiService.Update(id, topic));

    /// <summary>
    /// Delete topic
    /// </summary>
    /// <remarks>
    /// Permanently deletes a topic and all its comments.
    /// Only the topic author or moderators can delete topics.
    /// This action cannot be undone.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <response code="204">Topic deleted successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteTopic(Guid id)
    {
        await _topicApiService.Delete(id);
        return NoContent();
    }


    /// <summary>
    /// Like a topic
    /// </summary>
    /// <remarks>
    /// Adds a like to the topic from the current user.
    /// Users cannot like their own topics.
    /// Each user can only like a topic once.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <response code="201">Like added, returns user who liked</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot like this topic (e.g., own topic)</response>
    /// <response code="409">User already liked this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpPost("{id}/likes", Name = nameof(PostTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostTopicLike(Guid id) =>
        CreatedAtRoute(nameof(GetTopic), new {id}, await _likeApiService.LikeTopic(id));

    /// <summary>
    /// Remove like from topic
    /// </summary>
    /// <remarks>
    /// Removes the current user's like from the topic.
    /// Can only remove your own likes.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <response code="204">Like removed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot remove like (not their like)</response>
    /// <response code="409">User has not liked this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("{id}/likes", Name = nameof(DeleteTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteTopicLike(Guid id)
    {
        await _likeApiService.DislikeTopic(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all comments in topic as read
    /// </summary>
    /// <remarks>
    /// Marks all comments in this topic as read for the current user.
    /// Useful for clearing unread indicators without reading each comment.
    /// </remarks>
    /// <param name="id">Topic identifier (GUID)</param>
    /// <response code="204">All comments marked as read</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Topic not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(ReadTopicComments))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> ReadTopicComments(Guid id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }
}