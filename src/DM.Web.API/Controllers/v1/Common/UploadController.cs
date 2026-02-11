using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Common;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Common;

/// <summary>
/// File upload management with presigned URLs
/// </summary>
/// <remarks>
/// Provides secure file upload workflow using presigned URLs for direct S3/MinIO upload.
/// Flow: 1) Request presigned URL → 2) Upload file directly to storage → 3) Confirm upload
/// </remarks>
[ApiController]
[Route("v1/uploads")]
[ApiExplorerSettings(GroupName = "Common")]
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
    /// Admin options:
    /// - **scope=all**: Get all uploads across all users
    /// - **userLogin={login}**: Get uploads by specific user
    /// </remarks>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <param name="scope">Scope filter (use "all" for admin to see all uploads)</param>
    /// <param name="userLogin">Filter by user login (admin only)</param>
    /// <response code="200">List of uploads</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Admin access required for scope=all or userLogin filter</response>
    [HttpGet(Name = nameof(GetUploads))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Upload>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> GetUploads(
        [FromQuery] UploadsQuery query,
        [FromQuery] string? scope = null,
        [FromQuery] string? userLogin = null)
    {
        var all = scope?.Equals("all", StringComparison.OrdinalIgnoreCase) == true;
        var result = await _uploadApiService.GetUploads(query, userLogin, all);
        return Ok(result);
    }

    /// <summary>
    /// Get upload by ID
    /// </summary>
    /// <param name="id">Upload identifier</param>
    /// <response code="200">Upload details</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to view this upload</response>
    /// <response code="404">Upload not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Upload>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetUpload(Guid id)
    {
        var result = await _uploadApiService.GetUpload(id);
        return Ok(result);
    }

    /// <summary>
    /// Delete upload
    /// </summary>
    /// <param name="id">Upload identifier</param>
    /// <response code="204">Upload deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to delete this upload</response>
    /// <response code="404">Upload not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> DeleteUpload(Guid id)
    {
        await _uploadApiService.DeleteUpload(id);
        return NoContent();
    }

    /// <summary>
    /// Request presigned URL for direct upload
    /// </summary>
    /// <remarks>
    /// Returns a presigned URL that allows direct upload to storage.
    /// The URL expires after 15 minutes.
    /// After uploading, call the confirm endpoint to process the file.
    /// </remarks>
    /// <param name="request">Upload request details</param>
    /// <response code="200">Presigned URL for upload</response>
    /// <response code="400">Invalid request (file too large, invalid type)</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost("presign", Name = nameof(RequestPresignedUrl))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PresignResponse>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> RequestPresignedUrl([FromBody] PresignRequest request)
    {
        var result = await _uploadApiService.RequestPresignedUrl(request);
        return Ok(result);
    }

    /// <summary>
    /// Upload file directly with server-side processing
    /// </summary>
    /// <remarks>
    /// For small files (avatars, character portraits) that need server-side image processing.
    /// The file is uploaded, processed (thumbnails generated for images), and stored in one step.
    /// For large files, use the presigned URL workflow instead.
    ///
    /// Supported image types: JPEG, PNG, WebP, GIF. Max file size: 10 MB.
    ///
    /// After upload, use the returned upload ID to attach it to an entity
    /// (e.g., PATCH /v1/account with avatarUploadId).
    /// </remarks>
    /// <param name="file">Image file (multipart/form-data)</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    /// <response code="200">File uploaded, processed, and confirmed</response>
    /// <response code="400">Invalid file (wrong format, too large, not an image)</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost("direct", Name = nameof(DirectUpload))]
    [AuthenticationRequired]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(Envelope<Upload>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> DirectUpload(
        IFormFile file,
        [FromQuery] UploadType type,
        [FromQuery] Guid? targetId = null)
    {
        var result = await _uploadApiService.DirectUpload(file, type, targetId);
        return Ok(result);
    }

    /// <summary>
    /// Confirm upload completion
    /// </summary>
    /// <remarks>
    /// Call this after successfully uploading to the presigned URL.
    /// Triggers image processing (thumbnails, optimization) for image files.
    /// </remarks>
    /// <param name="id">Upload ID from presign response</param>
    /// <response code="200">Upload confirmed and processed</response>
    /// <response code="400">Upload processing failed or file not found in storage</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to confirm this upload</response>
    /// <response code="404">Upload not found</response>
    /// <response code="410">Upload session expired</response>
    [HttpPost("{id:guid}/confirm", Name = nameof(ConfirmUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Upload>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> ConfirmUpload(Guid id)
    {
        var result = await _uploadApiService.ConfirmUpload(id);
        return Ok(result);
    }

}
