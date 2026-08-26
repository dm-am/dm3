using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
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
    /// Moderator+ always, and otherwise whoever may see the entity the file hangs
    /// on — for a post attachment, whoever may read the post, which is the same
    /// rule and the same query that decides whether the post is visible at all.
    /// Nothing to sign in with is a valid state: an attachment in an open room of
    /// a public game is public, and one in a closed room is 404 to everybody else,
    /// signed in or not.
    ///
    /// The response is a download: Content-Disposition attachment and nosniff, so
    /// a file served from the site's own origin cannot be talked into executing
    /// there, and private caching only, so no shared cache keeps a copy of
    /// something whose audience was decided per caller.
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

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Private, because who may read this was decided for this caller and not
        // for the address. A shared cache holding the answer would serve it to the
        // next person who asks for the same URL.
        Response.Headers.CacheControl = "private, max-age=300";
        if (content.Length.HasValue)
        {
            Response.ContentLength = content.Length.Value;
        }

        // Attachment, never inline: the file is served from the site's own origin,
        // and a document rendered there runs with the site's privileges.
        return File(content.Content, content.ContentType, content.FileName);
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
    /// Multipart/form-data upload. Magic-byte validation and EXIF/IPTC/XMP strip
    /// for every type, then a single S3 PUT of the source file. Thumbnail variants
    /// are produced on-the-fly at serving time and are not stored.
    ///
    /// Avatars (UserAvatar, CharacterAvatar): JPEG, PNG, WebP, at least 50 px on
    /// each side, downscaled to 1024 px, maximum 10 MB.
    ///
    /// Post attachments: JPEG, PNG, WebP, GIF, stored at their own size with no
    /// floor, maximum 5 MB, at most three per post, and only by the post's own
    /// author. Documents are not accepted — see ImageProcessingDefaults for why.
    /// </remarks>
    /// <param name="file">File (multipart/form-data)</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    /// <response code="201">File uploaded, processed, and confirmed</response>
    /// <response code="400">Invalid file (wrong format, too large, not an image), or no upload type named</response>
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
        // The type is read from the query string. Model binding refuses every
        // value it is given and cannot read - "99", "0", a misspelt member -
        // with a 400 that names the parameter, so the only state that reaches
        // here unnamed is the one where the parameter was not sent at all: a
        // value type nothing bound keeps its default, and no member of this
        // enum carries zero. That is the easy mistake on a multipart request,
        // where the type goes in the form beside the file rather than in the
        // query, and it used to be answered with a 500 and a support token for
        // a request that was merely incomplete: the zero travelled to the
        // service, whose switch over the type had no arm for it.
        //
        // That throw stays what it is, an invariant of a service handed a type
        // the API has already vouched for. Refusing the request is this
        // method's job, and the refusal names the field that is missing.
        RequireKnownType(type);

        // 201 on an Idempotency-Key replay too: the cached answer is the record
        // this logical request created, and the service does not report which
        // call stored it.
        var result = await _uploadApiService.DirectUpload(file, type, targetId);
        return CreatedAtRoute(nameof(GetUpload), new { id = result.Id }, result);
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
            // Zero is the default a parameter nobody sent keeps, so it is the
            // one undefined value that means "not named" rather than "named
            // wrongly". Both are refusals of the same field and each says which
            // of the two it is.
            ["type"] = type == default ? "Не указан тип загрузки" : "Неизвестный тип загрузки",
        });
    }
}
