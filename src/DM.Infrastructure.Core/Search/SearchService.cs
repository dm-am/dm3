using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Search;

namespace DM.Infrastructure.Core.Search;

/// <inheritdoc />
internal class SearchService(
    ISearchEngineRepository searchEngineRepository,
    IIdentityProvider identityProvider)
    : ISearchService
{
    /// <inheritdoc />
    public async Task<(IEnumerable<FoundEntity> results, PagingResult paging)> Search(string query,
        IEnumerable<SearchEntityType> types, PagingQuery pagingQuery,
        UserRole searcherRole, Guid searcherUserId)
    {
        var pageSize = identityProvider.Current.Settings.Paging.EntitiesPerPage;
        if (string.IsNullOrWhiteSpace(query))
        {
            return (Enumerable.Empty<FoundEntity>(), PagingResult.Empty(pageSize));
        }

        var pagingData = new PagingData(pagingQuery, pageSize, int.MaxValue);

        // Exactly the searcher's own role, as a single-element set: a document
        // lists every role its access policy admits, so authorization is set
        // membership. The previous expression asked HasFlag of an enum that has no
        // [Flags] attribute and whose members are 0..6, so it derived an arbitrary
        // role set from the bit pattern of the value.
        var (entities, totalCount) = await searchEngineRepository.Search(
            query, types, pagingData, new[] { searcherRole }, searcherUserId);

        pagingData = new PagingData(pagingQuery, pageSize, totalCount);
        return (entities, pagingData.Result);
    }
}
