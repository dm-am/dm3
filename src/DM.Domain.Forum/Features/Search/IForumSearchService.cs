using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Search;

public interface IForumSearchService
{
    /// <summary>
    /// Search the forum content the current identity may read.
    /// </summary>
    Task<(IEnumerable<ForumSearchHit> hits, PagingResult paging)> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default);
}
