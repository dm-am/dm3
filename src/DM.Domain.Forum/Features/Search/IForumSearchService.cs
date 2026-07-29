using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Search;

/// <summary>
/// Full-text search over forum topics and comments.
/// </summary>
public interface IForumSearchService
{
    /// <summary>
    /// Search the forum content the current identity may read.
    /// </summary>
    /// <param name="query">Free-text query.</param>
    /// <param name="pagingQuery">Requested page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IEnumerable<ForumSearchHit> hits, PagingResult paging)> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default);
}
