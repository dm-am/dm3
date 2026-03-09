using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Search;
using DM.Workers.SearchIndexer.Grpc;
using Grpc.Core;
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
        var (results, paging) = await _searchService.Search(request.Query, entityTypes, pagingQuery);
        return new SearchResponse
        {
            Total = paging.TotalEntitiesCount,
            Entities = { results.Select(_mapper.Map<SearchResponse.Types.SearchResultEntity>) }
        };
    }
}