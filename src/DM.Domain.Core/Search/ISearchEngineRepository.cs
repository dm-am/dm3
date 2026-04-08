using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Search;

/// <summary>
/// Searchable entities storage
/// </summary>
public interface ISearchEngineRepository
{
    /// <summary>
    /// Search entities in index by text
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="searchEntityType">Search entity type</param>
    /// <param name="pagingData">Paging data</param>
    /// <param name="roles">Authenticated user roles</param>
    /// <param name="userId">Authenticated user identifier</param>
    /// <returns>Found entities with total count</returns>
    Task<(IEnumerable<FoundEntity> entities, int totalCount)> Search(string query,
        IEnumerable<SearchEntityType> searchEntityType,
        PagingData pagingData, IEnumerable<UserRole> roles, Guid userId);
}
