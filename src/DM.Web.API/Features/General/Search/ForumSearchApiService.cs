using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Search;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.General.Search;

/// <inheritdoc />
internal class ForumSearchApiService : IForumSearchApiService
{
    private readonly IForumSearchService _searchService;

    public ForumSearchApiService(IForumSearchService searchService)
    {
        _searchService = searchService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ForumSearchResult>> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default)
    {
        var (hits, paging) = await _searchService.Search(query, pagingQuery, ct);

        var rows = hits.Select(h => new ForumSearchResult
        {
            EntityType = h.EntityType,
            Id = h.Id,
            TopicId = h.TopicId,
            TopicNumber = h.TopicNumber,
            TopicTitle = h.TopicTitle,
            BoardId = h.BoardId,
            BoardTitle = h.BoardTitle,
            CreatedUtc = h.CreatedUtc,
            Snippet = h.Snippet,
        });

        return new ListEnvelope<ForumSearchResult>(rows, new PagingInfo(paging));
    }
}
