using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// File upload management.
/// </summary>
/// <remarks>
/// Direct upload flow only. The client sends multipart/form-data to POST /v1/uploads,
/// the server validates, processes and atomically puts to S3.
/// </remarks>
[ApiController]
[Route("v1/uploads")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Uploads")]
public class UploadController : ControllerBase
{
    private readonly IUploadApiService _uploadApiService;

    /// <inheritdoc />
    public UploadController(IUploadApiService uploadApiService)
    {
        _uploadApiService = uploadApiService;
    }

    /// <summary>
    /// Get uploads with optional filters
    /// </summary>
    /// <remarks>
    /// By default returns current user's uploads.
    ///
    /// Privileged options (moderator and above):
    /// - **allUsers=true**: Get all uploads across all users
    /// - **username={username}**: Get uploads by specific user
    /// </remarks>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <param name="allUsers">Everybody's uploads rather than the caller's own (moderator and above)</param>
    /// <param name="username">Filter by username (moderator and above)</param>
    /// <response code="200">List of uploads</response>
    /// <response code="400">allUsers is not a boolean</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Moderator+ required for allUsers and the username filter</response>
    [HttpGet(Name = nameof(GetUploads))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Shared.Dto.Upload>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUploads(
        [FromQuery] UploadsQuery query,
        [FromQuery] bool allUsers = false,
        [FromQuery] string? username = null)
    {
        // A boolean, because that is the question. Spelled "scope", it took any
        // word at all and read everything other than "all" as "no": scope=every
        // quietly answered with the caller's own uploads under the name of the
        // whole site.
        var (uploads, paging) = await _uploadApiService.GetUploads(query, username, allUsers);
        return Ok(new ListEnvelope<Shared.Dto.Upload>(uploads, paging));
    }

    /// <summary>
    /// Get upload by ID
    /// </summary>
    /// <remarks>
    /// The owner can view their own upload; viewing another user's upload
    /// requires the Moderator role or higher (aligned with the list and
    /// delete endpoints).
    /// </remarks>
    /// <param name="id">Upload identifier</param>
    /// <response code="200">Upload details</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not the owner and not a moderator+</response>
    /// <response code="404">Upload not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Shared.Dto.Upload), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUpload(Guid id)
    {
        var result = await _uploadApiService.GetUpload(id);
        return Ok(result);
    }

    /// <summary>
    /// Delete upload (soft-delete; S3 cleanup is done by the background GC)
    /// </summary>
    /// <param name="id">Upload identifier</param>
    /// <response code="204">Upload deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to delete this upload</response>
    /// <response code="404">Upload not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUpload(Guid id)
    {
        await _uploadApiService.DeleteUpload(id);
        return NoContent();
    }

    /// <summary>
    /// Upload file with server-side processing
    /// </summary>
    /// <remarks>
    /// Multipart/form-data upload. For images (UserAvatar, CharacterAvatar):
    /// magic-byte validation, EXIF/IPTC/XMP strip, downscale to 1024 px, a single
    /// S3 PUT of the source file. Thumbnail variants are produced on-the-fly at
    /// serving time and are not stored.
    ///
    /// Allowed formats: JPEG, PNG, WebP. Maximum 10 MB.
    /// </remarks>
    /// <param name="file">File (multipart/form-data)</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    /// <response code="201">File uploaded, processed, and confirmed</response>
    /// <response code="400">Invalid file (wrong format, too large, not an image)</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpPost(Name = nameof(DirectUpload))]
    [AuthenticationRequired]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(Shared.Dto.Upload), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DirectUpload(
        IFormFile file,
        [FromQuery] UploadType type,
        [FromQuery] Guid? targetId = null)
    {
        // 201 on an Idempotency-Key replay too: the cached answer is the record
        // this logical request created, and the service does not report which
        // call stored it.
        var result = await _uploadApiService.DirectUpload(file, type, targetId);
        return CreatedAtRoute(nameof(GetUpload), new { id = result.Id }, result);
    }
}
