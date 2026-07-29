using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Search;

/// <summary>
/// Postgres tsvector-backed full-text search over forum topics and comments.
/// Visibility is re-derived from the supplied access policy on every call.
/// </summary>
public interface IForumSearchRepository
{
    /// <summary>
    /// Run a relevance-ranked full-text search over every board the access
    /// policy admits.
    /// </summary>
    /// <param name="query">Free-text query.</param>
    /// <param name="accessPolicy">Composite board access policy of the searcher.</param>
    /// <param name="pagingData">Offset window over the ranked result set.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IEnumerable<ForumSearchHit> hits, int totalCount)> Search(
        string query, BoardAccessPolicy accessPolicy, PagingData pagingData, CancellationToken ct = default);
}
