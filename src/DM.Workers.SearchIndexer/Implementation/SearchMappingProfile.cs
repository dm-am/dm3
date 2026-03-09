using AutoMapper;
using AutoMapper.Extensions.EnumMapping;
using DM.Domain.Core.Search;
using DM.Workers.SearchIndexer.Grpc;

namespace DM.Workers.SearchIndexer.Implementation;

internal class SearchMappingProfile : Profile
{
    public SearchMappingProfile()
    {
        CreateMap<SearchEntityType, DM.Domain.Core.Enums.SearchEntityType>()
            .ConvertUsingEnumMapping()
            .ReverseMap();

        CreateMap<FoundEntity, SearchResponse.Types.SearchResultEntity>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.Id.ToString()));
    }
}