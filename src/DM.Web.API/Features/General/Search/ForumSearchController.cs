using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// Full-text search across forum topics and comments.
/// </summary>
/// <remarks>
/// Postgres tsvector-backed and live-consistent: which boards a caller may read
/// is derived from their role on every request, never stored alongside the
/// searchable text, so a board whose access policy changed is right on the next
/// search rather than after a reindex.
/// </remarks>
[ApiController]
[Route("v1/search")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Search")]
public class ForumSearchController : ControllerBase
{
    private readonly IForumSearchApiService _apiService;

    /// <inheritdoc />
    public ForumSearchController(IForumSearchApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Search forum topics and comments
    /// </summary>
    /// <remarks>
    /// Results are ranked by relevance: a match in a topic title outranks the
    /// same match in a topic body, which outranks it in a comment. The query is
    /// stemmed with the Russian dictionary, so "мастера" finds "мастер".
    ///
    /// Anonymous callers search the boards open to guests; everything else needs
    /// the role the board demands.
    /// </remarks>
    /// <param name="search">Search query string (1-500 characters)</param>
    /// <param name="paging">Pagination parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Search results with pagination</response>
    /// <response code="400">Invalid query (empty or too long)</response>
    /// <response code="429">Too many requests</response>
    [HttpGet("forum", Name = nameof(SearchForum))]
    [EnableRateLimiting(RateLimitPolicies.Sliding)]
    [ProducesResponseType(typeof(ListEnvelope<ForumSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    // The free-text filter is `search` on all thirteen other list endpoints;
    // this one used to call it `query` and its neighbour /v1/search/messages
    // called it `q`. See the query vocabulary in API_DESIGN.md.
    public async Task<IActionResult> SearchForum(
        [FromQuery][Required][StringLength(500, MinimumLength = 1)] string search,
        [FromQuery] PagingQuery paging,
        CancellationToken ct) =>
        Ok(await _apiService.Search(search, paging, ct));
}
