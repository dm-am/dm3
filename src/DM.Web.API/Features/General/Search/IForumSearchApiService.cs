using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.General.Search;

/// <summary>
/// Forum full-text search API service
/// </summary>
public interface IForumSearchApiService
{
    /// <summary>
    /// Search forum topics and comments
    /// </summary>
    /// <param name="query">Free-text query</param>
    /// <param name="pagingQuery">Requested page</param>
    /// <param name="ct">Cancellation token</param>
    Task<ListEnvelope<ForumSearchResult>> Search(
        string query, PagingQuery pagingQuery, CancellationToken ct = default);
}
