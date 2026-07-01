using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// File upload management.
/// </summary>
/// <remarks>
/// Direct upload flow only. Клиент шлет multipart/form-data на POST /v1/uploads,
/// сервер валидирует, процессит и атомарно кладет в S3.
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
    /// Admin options:
    /// - **scope=all**: Get all uploads across all users
    /// - **username={username}**: Get uploads by specific user
    /// </remarks>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <param name="scope">Scope filter (use "all" for admin to see all uploads)</param>
    /// <param name="username">Filter by username (admin only)</param>
    /// <response code="200">List of uploads</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Admin access required for scope=all or username filter</response>
    [HttpGet(Name = nameof(GetUploads))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Shared.Dto.Upload>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUploads(
        [FromQuery] UploadsQuery query,
        [FromQuery] string? scope = null,
        [FromQuery] string? username = null)
    {
        var all = scope?.Equals("all", StringComparison.OrdinalIgnoreCase) == true;
        var (uploads, paging) = await _uploadApiService.GetUploads(query, username, all);
        return Ok(new ListEnvelope<Shared.Dto.Upload>(uploads, paging));
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
    [ProducesResponseType(typeof(Shared.Dto.Upload), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUpload(Guid id)
    {
        var result = await _uploadApiService.GetUpload(id);
        return Ok(result);
    }

    /// <summary>
    /// Delete upload (soft-delete; S3 cleanup сделает фоновый GC)
    /// </summary>
    /// <param name="id">Upload identifier</param>
    /// <response code="204">Upload deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">Not allowed to delete this upload</response>
    /// <response code="404">Upload not found</response>
    [HttpDelete("{id:guid}", Name = nameof(DeleteUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUpload(Guid id)
    {
        await _uploadApiService.DeleteUpload(id);
        return NoContent();
    }

    /// <summary>
    /// Upload file with server-side processing
    /// </summary>
    /// <remarks>
    /// Multipart/form-data upload. Для изображений (UserAvatar, CharacterAvatar):
    /// magic-byte валидация, EXIF/IPTC/XMP strip, генерация WebP thumbnails
    /// (medium 400×400, small 100×100), атомарный batch S3 PUT.
    ///
    /// Допустимые форматы: JPEG, PNG, WebP. Максимум 10 МБ.
    /// </remarks>
    /// <param name="file">File (multipart/form-data)</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    /// <response code="200">File uploaded, processed, and confirmed</response>
    /// <response code="400">Invalid file (wrong format, too large, not an image)</response>
    /// <response code="401">User not authenticated</response>
    [HttpPost(Name = nameof(DirectUpload))]
    [AuthenticationRequired]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(Shared.Dto.Upload), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DirectUpload(
        IFormFile file,
        [FromQuery] UploadType type,
        [FromQuery] Guid? targetId = null)
    {
        var result = await _uploadApiService.DirectUpload(file, type, targetId);
        return Ok(result);
    }
}
