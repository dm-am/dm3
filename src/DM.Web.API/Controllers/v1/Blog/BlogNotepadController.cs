using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notepads;
using DM.Web.API.Services.Notepads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Blog;

/// <summary>
/// API controller for managing blog notepads
/// </summary>
/// <remarks>
/// Blog notepads allow blog owners and assistants to store shared notes for blog management
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Blog")]
[Tags("Blog Notepads")]
public class BlogNotepadController : ControllerBase
{
    private readonly INotepadApiService _notepadApiService;

    /// <inheritdoc />
    public BlogNotepadController(INotepadApiService notepadApiService)
    {
        _notepadApiService = notepadApiService;
    }

    /// <summary>
    /// Get blog notepad entries
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <response code="200">List of notepad entries</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    /// <response code="501">Not yet implemented</response>
    [HttpGet("blogs/{id}/notepad", Name = nameof(GetBlogNotepad))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<NotepadEntry>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 501)]
    public async Task<IActionResult> GetBlogNotepad(Guid id)
    {
        // TODO: Implement GetBlogEntries in INotepadApiService
        await Task.CompletedTask;
        return StatusCode(501, new GeneralError("Blog notepads not yet implemented"));
    }

    /// <summary>
    /// Create entry in blog notepad
    /// </summary>
    /// <param name="id">Blog identifier</param>
    /// <param name="request">Entry creation request</param>
    /// <response code="201">Entry created</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User must be blog owner or assistant</response>
    /// <response code="501">Not yet implemented</response>
    [HttpPost("blogs/{id}/notepad", Name = nameof(CreateBlogNotepadEntry))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotepadEntry>), 201)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 501)]
    public async Task<IActionResult> CreateBlogNotepadEntry(Guid id, [FromBody] CreateNotepadEntryRequest request)
    {
        // TODO: Implement CreateBlogEntry in INotepadApiService
        await Task.CompletedTask;
        return StatusCode(501, new GeneralError("Blog notepads not yet implemented"));
    }
}
