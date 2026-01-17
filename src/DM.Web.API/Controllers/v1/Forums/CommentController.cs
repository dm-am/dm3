using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;
using DM.Web.API.Dto.Users;
using DM.Web.API.Services.Boards;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Forums;

/// <inheritdoc />
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Forum")]
public class CommentController : ControllerBase
{
    private readonly ICommentApiService commentApiService;
    private readonly ILikeApiService likeApiService;

    /// <inheritdoc />
    public CommentController(
        ICommentApiService commentApiService,
        ILikeApiService likeApiService)
    {
        this.commentApiService = commentApiService;
        this.likeApiService = likeApiService;
    }

    /// <summary>
    /// Get list of comments in topic
    /// </summary>
    /// <param name="id"></param>
    /// <param name="q"></param>
    /// <response code="200"></response>
    /// <response code="410">Topic not found</response>
    [HttpGet("topics/{id}/comments", Name = nameof(GetForumComments))]
    [ProducesResponseType(typeof(ListEnvelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetForumComments(Guid id, [FromQuery] PagingQuery q) =>
        Ok(await commentApiService.Get(id, q));

    /// <summary>
    /// Create new comment in topic
    /// </summary>
    /// <param name="id"></param>
    /// <param name="comment">Comment</param>
    /// <response code="201"></response>
    /// <response code="400">Some of comment properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to comment this topic</response>
    /// <response code="410">Topic not found</response>
    [HttpPost("topics/{id}/comments", Name = nameof(PostForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostForumComment(Guid id, [FromBody] Comment comment)
    {
        var result = await commentApiService.Create(id, comment);
        return CreatedAtRoute(nameof(CommentController.GetForumComment), new { id = result.Resource.Id }, result);
    }


    /// <summary>
    /// Get forum comment
    /// </summary>
    /// <param name="id"></param>
    /// <response code="200"></response>
    /// <response code="410">Comment not found</response>
    [HttpGet("forum/comments/{id}", Name = nameof(GetForumComment))]
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetForumComment(Guid id) => Ok(await commentApiService.Get(id));

    /// <summary>
    /// Update forum comment
    /// </summary>
    /// <param name="id"></param>
    /// <param name="comment"></param>
    /// <response code="200"></response>
    /// <response code="400">Some changed comment properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPatch("forum/comments/{id}", Name = nameof(PatchForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Comment>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchForumComment(Guid id, [FromBody] Comment comment) =>
        Ok(await commentApiService.Update(id, comment));

    /// <summary>
    /// Delete forum comment
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to change this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("forum/comments/{id}", Name = nameof(DeleteForumComment))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteForumComment(Guid id)
    {
        await commentApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Add new like to forum comment
    /// </summary>
    /// <param name="id"></param>
    /// <response code="201"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to like the comment</response>
    /// <response code="409">User already liked this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpPost("forum/comments/{id}/likes", Name = nameof(PostForumCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<User>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostForumCommentLike(Guid id)
    {
        var result = await likeApiService.LikeComment(id);
        return CreatedAtRoute(nameof(GetForumComment), new {id}, result);
    }

    /// <summary>
    /// Delete like from forum comment
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove like from this comment</response>
    /// <response code="409">User has no like for this comment</response>
    /// <response code="410">Comment not found</response>
    [HttpDelete("forum/comments/{id}/likes", Name = nameof(DeleteForumCommentLike))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteForumCommentLike(Guid id)
    {
        await likeApiService.DislikeComment(id);
        return NoContent();
    }
}