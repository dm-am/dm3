using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Moderation.Tags;

/// <summary>
/// Tag management for moderators
/// </summary>
/// <remarks>
/// Provides CRUD operations for tags and tag groups.
/// Requires SeniorModerator role or higher.
/// </remarks>
[ApiController]
[Route("v1/moderation/tags")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Tag Management")]
[RequireRole(UserRole.SeniorModerator)]
public class TagController : ControllerBase
{
    private readonly ITagApiService _tagApiService;

    /// <inheritdoc />
    public TagController(ITagApiService tagApiService)
    {
        _tagApiService = tagApiService;
    }

    #region Tag Groups

    /// <summary>
    /// Get all tag groups
    /// </summary>
    /// <response code="200">List of tag groups</response>
    /// <response code="403">Insufficient permissions</response>
    [HttpGet("groups", Name = nameof(GetTagGroups))]
    [ProducesResponseType(typeof(ListEnvelope<TagGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTagGroups(CancellationToken ct)
    {
        return Ok(await _tagApiService.GetGroups(ct));
    }

    /// <summary>
    /// Get a tag group by ID
    /// </summary>
    /// <param name="groupId">Tag group ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Tag group</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag group not found</response>
    [HttpGet("groups/{groupId}", Name = nameof(GetTagGroup))]
    [ProducesResponseType(typeof(TagGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTagGroup(Guid groupId, CancellationToken ct)
    {
        return Ok(await _tagApiService.GetGroup(groupId, ct));
    }

    /// <summary>
    /// Create a new tag group
    /// </summary>
    /// <param name="request">Tag group data</param>
    /// <response code="201">Tag group created</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="409">Tag group with this title already exists</response>
    [HttpPost("groups", Name = nameof(CreateTagGroup))]
    [ProducesResponseType(typeof(TagGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTagGroup([FromBody] CreateTagGroupRequest request)
    {
        var group = await _tagApiService.CreateGroup(request);
        return CreatedAtAction(nameof(GetTagGroup), new { groupId = group.Id }, group);
    }

    /// <summary>
    /// Update a tag group
    /// </summary>
    /// <param name="groupId">Tag group ID</param>
    /// <param name="request">Updated tag group data</param>
    /// <response code="200">Tag group updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag group not found</response>
    /// <response code="409">Tag group with this title already exists</response>
    [HttpPut("groups/{groupId}", Name = nameof(UpdateTagGroup))]
    [ProducesResponseType(typeof(TagGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTagGroup(Guid groupId, [FromBody] UpdateTagGroupRequest request)
    {
        var group = await _tagApiService.UpdateGroup(groupId, request);
        return Ok(group);
    }

    /// <summary>
    /// Delete a tag group
    /// </summary>
    /// <remarks>
    /// Cannot delete a group that contains tags.
    /// </remarks>
    /// <param name="groupId">Tag group ID</param>
    /// <response code="204">Tag group deleted</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag group not found</response>
    /// <response code="409">Cannot delete group with tags</response>
    [HttpDelete("groups/{groupId}", Name = nameof(DeleteTagGroup))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTagGroup(Guid groupId)
    {
        await _tagApiService.DeleteGroup(groupId);
        return NoContent();
    }

    #endregion

    #region Tags

    /// <summary>
    /// Get all tags
    /// </summary>
    /// <response code="200">List of tags</response>
    /// <response code="403">Insufficient permissions</response>
    [HttpGet(Name = nameof(GetModerationTags))]
    [ProducesResponseType(typeof(ListEnvelope<Tag>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModerationTags(CancellationToken ct)
    {
        return Ok(await _tagApiService.GetTags(ct));
    }

    /// <summary>
    /// Get tags by group ID
    /// </summary>
    /// <param name="groupId">Tag group ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">List of tags in the group</response>
    /// <response code="403">Insufficient permissions</response>
    [HttpGet("groups/{groupId}/tags", Name = nameof(GetTagsByGroup))]
    [ProducesResponseType(typeof(ListEnvelope<Tag>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTagsByGroup(Guid groupId, CancellationToken ct)
    {
        return Ok(await _tagApiService.GetTagsByGroup(groupId, ct));
    }

    /// <summary>
    /// Get a tag by ID
    /// </summary>
    /// <param name="tagId">Tag ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Tag</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag not found</response>
    [HttpGet("{tagId}", Name = nameof(GetModerationTag))]
    [ProducesResponseType(typeof(Tag), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModerationTag(Guid tagId, CancellationToken ct)
    {
        return Ok(await _tagApiService.GetTag(tagId, ct));
    }

    /// <summary>
    /// Create a new tag
    /// </summary>
    /// <param name="request">Tag data</param>
    /// <response code="201">Tag created</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag group not found</response>
    /// <response code="409">Tag with this title already exists in the group</response>
    [HttpPost(Name = nameof(CreateModerationTag))]
    [ProducesResponseType(typeof(Tag), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateModerationTag([FromBody] CreateTagRequest request)
    {
        var tag = await _tagApiService.CreateTag(request);
        return CreatedAtAction(nameof(GetModerationTag), new { tagId = tag.Id }, tag);
    }

    /// <summary>
    /// Update a tag
    /// </summary>
    /// <param name="tagId">Tag ID</param>
    /// <param name="request">Updated tag data</param>
    /// <response code="200">Tag updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag or tag group not found</response>
    /// <response code="409">Tag with this title already exists in the group</response>
    [HttpPut("{tagId}", Name = nameof(UpdateModerationTag))]
    [ProducesResponseType(typeof(Tag), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateModerationTag(Guid tagId, [FromBody] UpdateTagRequest request)
    {
        var tag = await _tagApiService.UpdateTag(tagId, request);
        return Ok(tag);
    }

    /// <summary>
    /// Delete a tag
    /// </summary>
    /// <remarks>
    /// Cannot delete a tag that is used by any games.
    /// </remarks>
    /// <param name="tagId">Tag ID</param>
    /// <response code="204">Tag deleted</response>
    /// <response code="403">Insufficient permissions</response>
    /// <response code="404">Tag not found</response>
    /// <response code="409">Cannot delete tag used by games</response>
    [HttpDelete("{tagId}", Name = nameof(DeleteModerationTag))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteModerationTag(Guid tagId)
    {
        await _tagApiService.DeleteTag(tagId);
        return NoContent();
    }

    #endregion
}
