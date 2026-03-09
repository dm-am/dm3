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

        CreateMap<string, PostBbText>().ConvertUsing(v => new PostBbText {Value = v});
        CreateMap<string, CommonBbText>().ConvertUsing(v => new CommonBbText {Value = v});
        CreateMap<string, InfoBbText>().ConvertUsing(v => new InfoBbText {Value = v});
    }
}