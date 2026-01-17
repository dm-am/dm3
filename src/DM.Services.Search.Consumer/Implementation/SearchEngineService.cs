using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.Search.BusinessProcesses;
using DM.Services.Search.Grpc;
using Grpc.Core;
using SearchEntityType = DM.Services.Core.Dto.Enums.SearchEntityType;

namespace DM.Services.Search.Consumer.Implementation;

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
        var pagingQuery = new PagingQuery { Skip = request.Skip, Size = request.Size };
        var entityTypes = request.SearchAcross.Select(t => _mapper.Map<SearchEntityType>(t));
        var (results, paging) = await _searchService.Search(request.Query, entityTypes, pagingQuery);
        return new SearchResponse
        {
            Total = paging.TotalEntitiesCount,
            Entities = { results.Select(_mapper.Map<SearchResponse.Types.SearchResultEntity>) }
        };
    }
}