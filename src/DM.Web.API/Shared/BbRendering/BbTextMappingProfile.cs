using AutoMapper;

namespace DM.Web.API.Shared.BbRendering;

/// <inheritdoc />
internal class BbTextMappingProfile : Profile
{
    /// <inheritdoc />
    public BbTextMappingProfile()
    {
        CreateMap<BbText, string?>()
            .IncludeAllDerived()
            .ConvertUsing(v => v == null ? null : v.Value);

        // Single CreateMap per runtime type pair. `string?` and `string` share
        // the runtime type (System.String), so registering both produces
        // AutoMapper.DuplicateTypeMapConfigurationException during
        // AssertConfigurationIsValid. The null-safe converters below cover
        // both nullable and non-nullable source parameters at the call site.
        CreateMap<string, PostBbText>()
            .ConvertUsing(v => v == null ? null! : new PostBbText { Value = v });
        CreateMap<string, CommonBbText>()
            .ConvertUsing(v => v == null ? null! : new CommonBbText { Value = v });
        CreateMap<string, InfoBbText>()
            .ConvertUsing(v => v == null ? null! : new InfoBbText { Value = v });
    }
}
