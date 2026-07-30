using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Comments;
using DM.Web.API.Features.Forum.Likes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Forum.Topics;

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
    private readonly ITopicLikeApiService _likeApiService;
    private readonly IForumCommentApiService _commentApiService;

    /// <summary>
    /// Creates a new instance of TopicController
    /// </summary>
    public TopicController(
        ITopicApiService topicApiService,
        ITopicLikeApiService likeApiService,
        IForumCommentApiService commentApiService)
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
    /// <response code="404">Board not found</response>
    [HttpGet("~/v1/boards/{id}/topics", Name = nameof(GetBoardTopics))]
    [ProducesResponseType(typeof(ListEnvelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBoardTopics(string id, [FromQuery] TopicsQuery q) =>
        Ok(await _topicApiService.Get(id, q));

    /// <summary>
    /// Get topics across all boards
    /// </summary>
    /// <remarks>
    /// Cross-board topic search — primarily used by the user-profile
    /// "Topics" tab, which scopes results with the <c>authors</c> filter
    /// to render every topic authored by a given user.
    ///
    /// Access policy is enforced on the server: topics on boards the
    /// viewer cannot see never appear in the response.
    ///
    /// Supports the same filters and sort options as the per-board
    /// endpoint: search, authors, createdFromUtc, createdToUtc,
    /// sortBy (lastActivity / created / comments / title / likes),
    /// sortOrder (asc / desc), and standard paging (number / size).
    /// </remarks>
    /// <param name="q">Filter, sort and paging parameters</param>
    /// <response code="200">Paginated list of topics</response>
    /// <response code="400">Invalid query parameters</response>
    [HttpGet("", Name = nameof(GetTopics))]
    [ProducesResponseType(typeof(ListEnvelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopics([FromQuery] TopicsQuery q) =>
        Ok(await _topicApiService.GetAcrossBoards(q));

    /// <summary>
    /// Get the user's most-liked topic (profile widget)
    /// </summary>
    /// <remarks>
    /// Returns the single highest-liked topic authored by the user, across
    /// every board the viewer can see. Used by the profile page's "Topics"
    /// tab to spotlight the user's best topic — the forum counterpart of
    /// <c>GET /v1/users/{username}/best-publication</c>.
    ///
    /// Tie-breaker on equal like counts is creation time (newer first).
    /// Access policy is enforced server-side: topics on boards the viewer
    /// cannot see never appear.
    /// </remarks>
    /// <param name="username">Author username</param>
    /// <response code="200">Envelope with the topic, or <c>resource: null</c> if the user has none</response>
    /// <response code="404">User not found</response>
    [HttpGet("~/v1/users/{username}/best-topic", Name = nameof(GetUserBestTopic))]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserBestTopic(string username) =>
        Ok(await _topicApiService.GetUserBestTopic(username));

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
    /// <response code="404">Board not found</response>
    [HttpPost("~/v1/boards/{id}/topics", Name = nameof(PostBoardTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    /// <response code="404">Topic not found</response>
    [HttpGet("{id}", Name = nameof(GetTopic))]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopic(Guid id) => Ok(await _topicApiService.Get(id));

    /// <summary>
    /// Get topic by board alias and topic number
    /// </summary>
    /// <remarks>
    /// Alternative way to get a topic using human-readable URL path.
    /// The topic number is stable within a board (assigned at creation time).
    /// </remarks>
    /// <param name="alias">Board URL alias (lowercase, e.g. "general")</param>
    /// <param name="num">Topic number within the board</param>
    /// <response code="200">Topic details</response>
    /// <response code="404">Board or topic not found</response>
    [HttpGet("~/v1/forum/{alias}/{num:int}", Name = nameof(GetTopicByNumber))]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicByNumber(string alias, int num) =>
        Ok(await _topicApiService.GetByBoardAndNumber(alias, num));

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
    /// <response code="404">Topic not found</response>
    [HttpGet("{id}/discussion", Name = nameof(GetTopicDiscussion))]
    [ProducesResponseType(typeof(DiscussionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    /// <param name="request">Fields to change</param>
    /// <response code="200">Updated topic</response>
    /// <response code="400">Invalid update data</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User cannot modify this topic or specific fields</response>
    /// <response code="404">Topic not found</response>
    [HttpPatch("{id}", Name = nameof(PatchTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTopic(Guid id, [FromBody] UpdateTopicRequest request) =>
        Ok(await _topicApiService.Update(id, request));

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
    /// <response code="404">Topic not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteTopic))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    /// <response code="404">Topic not found</response>
    [HttpPost("{id}/likes", Name = nameof(PostTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
    /// <response code="404">Topic not found</response>
    [HttpDelete("{id}/likes", Name = nameof(DeleteTopicLike))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTopicLike(Guid id)
    {
        await _likeApiService.UnlikeTopic(id);
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
    /// <response code="404">Topic not found</response>
    [HttpDelete("{id}/comments/unread", Name = nameof(ReadTopicComments))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReadTopicComments(Guid id)
    {
        await _commentApiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Reorder pinned topics in board
    /// </summary>
    /// <remarks>
    /// Updates the display order of pinned topics.
    /// The first topic ID in the array will appear first (top).
    /// Only board moderators and administrators can perform this action.
    /// </remarks>
    /// <param name="id">Board identifier (GUID or URL slug)</param>
    /// <param name="request">Reorder request with topic IDs in desired order</param>
    /// <response code="204">Topics reordered successfully</response>
    /// <response code="400">Invalid request (empty array, invalid IDs)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not a board moderator</response>
    /// <response code="404">Board not found</response>
    [HttpPatch("~/v1/boards/{id}/topics/pinned/order", Name = nameof(ReorderPinnedTopics))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderPinnedTopics(string id, [FromBody] ReorderPinnedRequest request)
    {
        await _topicApiService.ReorderPinned(id, request);
        return NoContent();
    }
}
