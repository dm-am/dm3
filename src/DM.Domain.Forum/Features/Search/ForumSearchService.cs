using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Forum.Features.Boards;

namespace DM.Domain.Forum.Features.Search;

/// <inheritdoc />
internal class ForumSearchService(
    IForumSearchRepository searchRepository,
    IAccessPolicyConverter accessPolicyConverter,
    IIdentityProvider identityProvider)
    : IForumSearchService
{
    /// <inheritdoc />
    public async Task<(IEnumerable<ForumSearchHit> hits, PagingResult paging)> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default)
    {
        var identity = identityProvider.Current;
        var pageSize = identity.Settings.Paging.EntitiesPerPage;

        if (string.IsNullOrWhiteSpace(query))
        {
            return (Enumerable.Empty<ForumSearchHit>(), PagingResult.Empty(pageSize));
        }

        // The searcher's board access policy, derived from their role by the same
        // converter every other forum read path uses. Nothing about visibility is
        // stored with the searchable text: it is re-derived per request, so a
        // board whose policy changed is right on the very next search.
        var accessPolicy = accessPolicyConverter.Convert(identity.User.Role);

        // Total is unknown before the query runs, so the window is built twice:
        // once to carry skip/take into SQL, once to turn the count SQL returned
        // into the page descriptor. Same shape the rest of the paged reads use.
        var pagingData = new PagingData(pagingQuery, pageSize, int.MaxValue);
        var (hits, totalCount) = await searchRepository.Search(query.Trim(), accessPolicy, pagingData, ct);

        return (hits, new PagingData(pagingQuery, pageSize, totalCount).Result);
    }
}
