using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;
using UploadDto = DM.Web.API.Shared.Dto.Upload;

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
    private readonly UploadApiService _uploadApiService;

    public UploadController(UploadApiService uploadApiService)
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
    [ProducesResponseType(typeof(ListEnvelope<UploadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUploads(
        [FromQuery] UploadsQuery query,
        [FromQuery] bool allUsers = false,
        [FromQuery] string? username = null)
    {
        var (uploads, paging) = await _uploadApiService.GetUploads(query, username, allUsers);
        return Ok(new ListEnvelope<UploadDto>(uploads, paging));
    }

    /// <summary>
    /// Get upload by ID
    /// </summary>
    /// <remarks>
    /// The owner can view their own upload; viewing another user's upload
    /// requires the Moderator role or higher (aligned with the list and
    /// delete endpoints). A file the caller may not have is answered as one that
    /// does not exist, as its bytes are.
    /// </remarks>
    /// <param name="id">Upload identifier</param>
    /// <response code="200">Upload details</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">The caller reads the file, and is neither its owner nor a moderator+</response>
    /// <response code="404">No such file, or the caller may not have it</response>
    [HttpGet("{id:guid}", Name = nameof(GetUpload))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(UploadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUpload(Guid id) => Ok(await _uploadApiService.GetUpload(id));

    /// <summary>
    /// Get the file itself
    /// </summary>
    /// <remarks>
    /// The only way the bytes of a file on a closed prefix ever reach a browser.
    /// The bucket answers anonymous reads on the avatar prefixes alone, and no
    /// signed or direct address is ever handed out for the rest — a link that
    /// works because it is known is a pass to whoever comes to hold it, and would
    /// take an attachment out of the private room it belongs to for good.
    ///
    /// So the right is decided here, per request: the owner of the file and
    /// Moderator+ wherever the entity is, and otherwise whoever may see the entity
    /// the file hangs on — for a post attachment, whoever may read the post, which
    /// is the same rule and the same query that decides whether the post is visible
    /// at all. A file marked for chosen characters, or one the game text shows only
    /// inside hidden blocks, goes, for everybody, Moderator+ included, only to whoever
    /// reads a hidden block with the same addressees, or one of those blocks.
    /// Nothing to sign in with is a valid state: an attachment in an open room of
    /// a public game is public, and one in a closed room is 404 to everybody else,
    /// signed in or not.
    ///
    /// The response is a download: Content-Disposition attachment and nosniff, so
    /// a file served from the site's own origin cannot be talked into executing
    /// there, and private caching only, so no shared cache keeps a copy of
    /// something whose audience was decided per caller. Even the browser keeps a
    /// copy only of a file a guest reads too: anything else is no-store, so a
    /// second account signed in the same browser is not served the first one's.
    /// </remarks>
    /// <param name="id">Upload identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">File content</response>
    /// <response code="404">No such file, or the caller may not have it</response>
    [HttpGet("{id:guid}/content", Name = nameof(GetUploadContent))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUploadContent(Guid id, CancellationToken ct)
    {
        var content = await _uploadApiService.GetUploadContent(id, ct);

        // Private: who may read this was decided for this caller, not for the address.
        // Kept by the browser only when every caller gets it: a copy kept for one
        // account is served to the next one signed in the same browser.
        // nosniff is set for every response by SecurityHeadersMiddleware.
        Response.Headers.CacheControl = content.OpenToGuest ? "private, max-age=300" : "no-store";
        if (content.Length.HasValue)
        {
            Response.ContentLength = content.Length.Value;
        }

        // Attachment, never inline: the file is served from the site's own origin,
        // and a document rendered there runs with the site's privileges.
        return File(content.Content, content.ContentType, content.FileName);
    }

    /// <summary>
    /// Change who sees a file of a post
    /// </summary>
    /// <remarks>
    /// Only the author of the post, on a file of the post. "Selected" with no
    /// characters leaves the file to the author and the leads. Acts from the next
    /// request: a browser that opened the file keeps it for as long as the private
    /// cache of the content endpoint does.
    /// </remarks>
    /// <param name="id">Upload identifier</param>
    /// <param name="request">Who sees the file</param>
    /// <response code="200">The upload with its new mark</response>
    /// <response code="400">No choice of who sees the file, not a file of a post, a character not let into the room, or "All" for a file the game text puts only inside hidden blocks</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">The caller reads the file and its post, and is not the author of the post</response>
    /// <response code="404">No such file, or the caller may not have it or read its post</response>
    [HttpPatch("{id:guid}", Name = nameof(PatchUpload))]
    [AuthenticationRequired]
    [EnableRateLimiting(RateLimitPolicies.Default)]
    [ProducesResponseType(typeof(Envelope<UploadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchUpload(Guid id, [FromBody] UpdateUploadAudienceRequest request)
    {
        var visibleTo = request.VisibleTo ?? throw new HttpBadRequestException(new Dictionary<string, string>
        {
            ["visibleTo"] = RefusalMessage.AudienceNotChosen,
        });
        RequireKnownAudience(visibleTo);
        return Ok(new Envelope<UploadDto>(
            await _uploadApiService.UpdateAudience(id, Audience(visibleTo, request.CharacterIds))));
    }

    /// <summary>
    /// Delete upload (soft-delete; S3 cleanup is done by the background GC)
    /// </summary>
    /// <param name="id">Upload identifier</param>
    /// <response code="204">Upload deleted</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">The caller reads the file, and is not allowed to delete it</response>
    /// <response code="404">No such file, or the caller may not have it</response>
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
    /// Multipart/form-data upload. Magic-byte validation and EXIF/IPTC/XMP strip
    /// for every type, then an S3 PUT of the source file. An avatar also gets its
    /// square thumbnails cut at upload and stored beside the source.
    ///
    /// Avatars (UserAvatar, CharacterAvatar): JPEG, PNG, WebP, at least 50 px on
    /// each side, downscaled to 1024 px, maximum 10 MB.
    ///
    /// Post attachments: JPEG, PNG, WebP, GIF, stored at their own size with no
    /// floor, maximum 5 MB, at most three per post, and only by the post's own
    /// author. Documents are not accepted — see ImageProcessingDefaults for why.
    /// Who sees an attachment is chosen with it and stored in the same insert, so
    /// the file is never visible wider than chosen; the default is every reader.
    /// </remarks>
    /// <param name="file">File (multipart/form-data)</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    /// <param name="visibleTo">Who sees a file of a post: "All" (default) or "Selected"</param>
    /// <param name="characterIds">For "Selected": the characters chosen, repeated per character</param>
    /// <response code="201">File uploaded, processed, and confirmed</response>
    /// <response code="400">Invalid file (wrong format, too large, not an image), no upload type named, "Selected" on an avatar, or a character not let into the room</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpPost(Name = nameof(DirectUpload))]
    [AuthenticationRequired]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DirectUpload(
        IFormFile file,
        [FromQuery] UploadType type,
        [FromQuery] Guid? targetId = null,
        [FromForm] UploadVisibleTo visibleTo = UploadVisibleTo.All,
        // Named, so a form without the field binds no characters: unnamed, the binder
        // falls back to the empty prefix whenever the form has another field
        // (visibleTo), reads "[0]" as a character and refuses the upload with 400.
        [FromForm(Name = "characterIds")] Guid[]? characterIds = null)
    {
        // A type not sent keeps its zero default, which no member carries: refused
        // here by name, before the service's switch sees it.
        RequireKnownType(type);
        RequireKnownAudience(visibleTo);

        // 201 on an Idempotency-Key replay too: the cached answer is the record
        // this logical request created, and the service does not report which
        // call stored it.
        var result = await _uploadApiService.DirectUpload(file, type, targetId, Audience(visibleTo, characterIds));
        return CreatedAtRoute(nameof(GetUpload), new { id = result.Id }, result);
    }

    private static UploadAudience Audience(UploadVisibleTo visibleTo, IReadOnlyCollection<Guid>? characterIds) =>
        visibleTo == UploadVisibleTo.All ? UploadAudience.All : new UploadAudience(visibleTo, characterIds ?? []);

    /// <summary>
    /// Refuses a choice of who sees a file that names neither of the two.
    /// </summary>
    private static void RequireKnownAudience(UploadVisibleTo visibleTo)
    {
        if (!Enum.IsDefined(visibleTo))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                ["visibleTo"] = "Неизвестный выбор, кто видит файл",
            });
        }
    }

    /// <summary>
    /// Refuses an upload that names no upload type, or names one this site does
    /// not have.
    /// </summary>
    private static void RequireKnownType(UploadType type)
    {
        if (Enum.IsDefined(type))
        {
            return;
        }

        throw new HttpBadRequestException(new Dictionary<string, string>
        {
            // Zero means "not named", any other undefined value "named wrongly".
            ["type"] = type == default ? "Не указан тип загрузки" : "Неизвестный тип загрузки",
        });
    }
}
