using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Search;

/// <summary>
/// Service for search requests
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Look through entities
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="types">Entity types</param>
    /// <param name="pagingQuery">Paging query</param>
    /// <param name="searcherRole">Role of the user performing the search</param>
    /// <param name="searcherUserId">Identifier of the user performing the search</param>
    /// <returns>Search results with paging information</returns>
    /// <remarks>
    /// The searcher is passed in rather than read from an ambient identity: the
    /// only host of this service is the indexing worker, which has no session and
    /// would authorize every search as a guest.
    /// </remarks>
    Task<(IEnumerable<FoundEntity> results, PagingResult paging)> Search(string query,
        IEnumerable<SearchEntityType> types, PagingQuery pagingQuery,
        UserRole searcherRole, Guid searcherUserId);
}
