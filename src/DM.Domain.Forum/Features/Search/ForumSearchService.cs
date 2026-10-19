using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;

namespace DM.Domain.Forum.Features.Search;

/// <inheritdoc />
internal class ForumSearchService : IForumSearchService
{
    private readonly IForumSearchRepository _searchRepository;
    private readonly IIdentityProvider _identityProvider;

    public ForumSearchService(
        IForumSearchRepository searchRepository,
        IIdentityProvider identityProvider)
    {
        _searchRepository = searchRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ForumSearchHit> hits, PagingResult paging)> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        var pageSize = identity.Settings.PageSize;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ([], PagingResult.Empty(pageSize));
        }

        // The total is unknown before the query runs, so the window is built twice.
        var pagingData = new PagingData(pagingQuery, pageSize, int.MaxValue);
        var (hits, totalCount) = await _searchRepository.Search(query.Trim(), pagingData, ct);

        return (hits, new PagingData(pagingQuery, pageSize, totalCount).Result);
    }
}
