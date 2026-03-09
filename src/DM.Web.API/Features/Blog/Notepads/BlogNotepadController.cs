using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Blog.Notepads;

/// <summary>
/// API controller for managing blog notepads
/// </summary>
/// <remarks>
/// Blog notepads allow blog owners and assistants to store shared notes for blog management
/// </remarks>
[ApiController]
[Route("v1/blogs/{blogId}/notepad")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Blog Notepads")]
[AuthenticationRequired]
public class BlogNotepadController : ControllerBase
{
    private readonly IBlogNotepadApiService _notepadApiService;

    /// <inheritdoc />
    public BlogNotepadController(IBlogNotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get blog notepad entries
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    [HttpGet(Name = nameof(GetBlogNotepad))]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBlogNotepad(Guid blogId) =>
        Ok(await _notepadApiService.GetEntries(blogId));

    /// <summary>
    /// Create entry in blog notepad
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    [HttpPost(Name = nameof(CreateBlogNotepadEntry))]
    [ProducesResponseType(typeof(Envelope<NotepadEntryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBlogNotepadEntry(Guid blogId, [FromBody] CreateNotepadEntryRequest request)
    {
        var result = await _notepadApiService.CreateEntry(blogId, request);
        return CreatedAtRoute(nameof(GetBlogNotepadEntry), new { blogId, entryId = result.Resource.Id }, result);
    }

    /// <summary>
    /// Get blog notepad entry by ID
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="200">Entry details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{entryId:guid}", Name = nameof(GetBlogNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlogNotepadEntry(Guid blogId, Guid entryId) =>
        Ok(await _notepadApiService.GetEntry(entryId));

    /// <summary>
    /// Update blog notepad entry
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Entry updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    /// <response code="404">Entry not found</response>
    [HttpPatch("{entryId:guid}", Name = nameof(UpdateBlogNotepadEntry))]
    [ProducesResponseType(typeof(NotepadEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBlogNotepadEntry(Guid blogId, Guid entryId, [FromBody] UpdateNotepadEntryRequest request) =>
        Ok(await _notepadApiService.UpdateEntry(entryId, request));

    /// <summary>
    /// Delete blog notepad entry
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <param name="entryId">Entry identifier</param>
    /// <response code="204">Entry deleted</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    [HttpDelete("{entryId:guid}", Name = nameof(DeleteBlogNotepadEntry))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteBlogNotepadEntry(Guid blogId, Guid entryId)
    {
        await _notepadApiService.DeleteEntry(entryId);
        return NoContent();
    }
}
