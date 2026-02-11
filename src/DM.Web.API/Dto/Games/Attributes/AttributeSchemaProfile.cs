using AutoMapper;

namespace DM.Web.API.Dto.Games.Attributes;

/// <inheritdoc />
internal class AttributeSchemaProfile : Profile
{
    /// <inheritdoc />
    public AttributeSchemaProfile()
    {
        CreateMap<DM.Services.Game.Dto.Shared.AttributeSchema, AttributeSchema>()
            .ReverseMap();

        CreateMap<DM.Services.Game.Dto.Shared.AttributeSpecification, AttributeSpecification>()
            .ReverseMap();

        CreateMap<DM.Services.Game.Dto.Shared.ListValue, AttributeValueSpecification>()
            .ReverseMap();
    }
}