using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Workers.SearchIndexer.Grpc;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// Full-text search across website content
/// </summary>
/// <remarks>
/// Searches across users, games, and posts using Elasticsearch-powered backend.
/// Results are ranked by relevance and paginated.
/// </remarks>
[ApiController]
[Route("v1/search")]
[ApiExplorerSettings(GroupName = "General")]
[Tags("Search")]
public class SearchController : ControllerBase
{
    private readonly SearchEngine.SearchEngineClient _searchClient;

    /// <inheritdoc />
    public SearchController(SearchEngine.SearchEngineClient searchClient)
    {
        _searchClient = searchClient;
    }

    /// <summary>
    /// Search across all content types
    /// </summary>
    /// <remarks>
    /// Performs a full-text search across users, games, and posts.
    /// Results are sorted by relevance score.
    ///
    /// ## Search Tips
    /// - Use quotes for exact phrases: "sword master"
    /// - Partial words are supported via prefix matching
    /// - Results include snippet previews with highlighted matches
    ///
    /// ## Rate Limits
    /// Search is rate-limited to prevent abuse. Heavy users may receive 429 responses.
    /// </remarks>
    /// <param name="query">Search query string (1-500 characters)</param>
    /// <param name="q">Pagination parameters</param>
    /// <response code="200">Search results with pagination</response>
    /// <response code="400">Invalid query (empty or too long)</response>
    /// <response code="503">Search service unavailable</response>
    [HttpGet(Name = nameof(Search))]
    [ProducesResponseType(typeof(ListEnvelope<SearchEntity>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromQuery][Required][StringLength(500, MinimumLength = 1)] string query,
        [FromQuery] PagingQuery q)
    {
        try
        {
            var searchResponse = await _searchClient.SearchAsync(new SearchRequest
            {
                Query = query,
                Skip = q.Skip,
                Size = q.Take,
            });

            var entities = searchResponse.Entities.Select(e => new SearchEntity
            {
                Id = e.Id,
                Type = e.Type.ToString(),
                Title = e.OriginalTitle,
                Preview = e.FoundText
            });

            var paging = new PagingInfo(PagingResult.Create(searchResponse.Total, q.Skip + 1, q.Take));
            return Ok(new ListEnvelope<SearchEntity>(entities, paging));
        }
        catch (Grpc.Core.RpcException)
        {
            throw new HttpException(HttpStatusCode.ServiceUnavailable, "Search service is temporarily unavailable");
        }
    }
}

/// <summary>
/// Search result entity from the search service
/// </summary>
public class SearchEntity
{
    /// <summary>
    /// Entity type (User, Game, ForumTopic, ForumComment)
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Entity unique identifier
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Entity title or name
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Text preview with search term highlights
    /// </summary>
    public string? Preview { get; set; }
}
