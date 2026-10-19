using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Search;

/// <summary>
/// Postgres tsvector-backed full-text search over forum topics and comments.
/// </summary>
public interface IForumSearchRepository
{
    /// <summary>
    /// Run a relevance-ranked full-text search over every board.
    /// </summary>
    Task<(IEnumerable<ForumSearchHit> hits, int totalCount)> Search(
        string query, PagingData pagingData, CancellationToken ct = default);
}
