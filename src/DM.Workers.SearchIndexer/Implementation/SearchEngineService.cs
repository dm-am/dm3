using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Search;
using DM.Workers.SearchIndexer.Grpc;
using Grpc.Core;
using DM.Domain.Core.Enums;
using CoreSearchEntityType = DM.Domain.Core.Enums.SearchEntityType;

namespace DM.Workers.SearchIndexer.Implementation;

/// <inheritdoc />
public class SearchEngineService : SearchEngine.SearchEngineBase
{
    private readonly ISearchService _searchService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public SearchEngineService(
        ISearchService searchService,
        IMapper mapper)
    {
        _searchService = searchService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public override async Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
    {
        var pagingQuery = new PagingQuery { Skip = request.Skip, Take = request.Size };
        var entityTypes = request.SearchAcross.Select(t => _mapper.Map<CoreSearchEntityType>(t));
        // The worker has no session: an unparsable or absent identity means the
        // caller is treated as a guest, never as more.
        var searcherRole = Enum.IsDefined(typeof(UserRole), request.SearcherRole)
            ? (UserRole)request.SearcherRole
            : UserRole.Guest;
        var searcherUserId = Guid.TryParse(request.SearcherUserId, out var parsed) ? parsed : Guid.Empty;

        var (results, paging) = await _searchService.Search(
            request.Query, entityTypes, pagingQuery, searcherRole, searcherUserId);
        return new SearchResponse
        {
            Total = paging.TotalEntitiesCount,
            Entities = { results.Select(_mapper.Map<SearchResponse.Types.SearchResultEntity>) }
        };
    }
}