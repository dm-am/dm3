using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DM.Web.API.Features.Search;

/// <summary>
/// Unified full-text search across chat messages and game posts.
/// </summary>
/// <remarks>
/// Postgres tsvector-backed, live-consistent search over every source the
/// authenticated user may read: the global chat, their direct/group chats, and
/// posts in games they can access. Private [private] game blocks are stripped
/// before indexing and are never matched or previewed.
/// </remarks>
[ApiController]
[Route("v1/search")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Search")]
public class MessageSearchController : ControllerBase
{
    private readonly IMessageSearchApiService _apiService;

    /// <inheritdoc />
    public MessageSearchController(IMessageSearchApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Search messages and game posts
    /// </summary>
    /// <remarks>
    /// Default sort is CreatedUtc DESC with opaque keyset pagination. Access is
    /// re-validated on every page from the authenticated identity.
    ///
    /// ## Operators
    /// Inline operators inside <c>q</c> are also accepted as separate query
    /// params: <c>from:&lt;username&gt;</c>, <c>before:</c>/<c>after:</c>/<c>during:&lt;date&gt;</c>.
    ///
    /// ## Scopes (<c>in</c>, repeatable)
    /// <c>in=global</c>, <c>in=dm:&lt;chatId|publicId&gt;</c>, <c>in=game:&lt;gameId|publicId&gt;</c>.
    /// Omitted (or <c>in=all</c>) searches everything accessible. A scope the
    /// user cannot access yields no rows.
    /// </remarks>
    /// <param name="q">Search query string (1-500 characters, required)</param>
    /// <param name="in">Repeatable scope filter</param>
    /// <param name="from">Author username filter (case-insensitive)</param>
    /// <param name="after">Inclusive lower bound on creation time (UTC)</param>
    /// <param name="before">Inclusive upper bound on creation time (UTC)</param>
    /// <param name="cursor">Opaque cursor for the next page</param>
    /// <param name="limit">Maximum number of results (1-100, default 50)</param>
    /// <response code="200">Search results with cursor pagination</response>
    /// <response code="400">Invalid query (empty or too long)</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="429">Too many requests</response>
    [HttpGet("messages", Name = nameof(SearchMessages))]
    [AuthenticationRequired]
    [EnableRateLimiting("sliding")]
    [ProducesResponseType(typeof(CursorEnvelope<MessageSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SearchMessages(
        [FromQuery][Required][StringLength(500, MinimumLength = 1)] string q,
        [FromQuery(Name = "in")] string[]? @in = null,
        [FromQuery] string? from = null,
        [FromQuery] DateTimeOffset? after = null,
        [FromQuery] DateTimeOffset? before = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50) =>
        Ok(await _apiService.SearchAsync(q, @in ?? Array.Empty<string>(), from, after, before, cursor, limit));
}
