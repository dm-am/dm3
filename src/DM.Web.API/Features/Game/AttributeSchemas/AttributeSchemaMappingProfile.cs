using AutoMapper;
using DomainAttributeSchema = DM.Domain.Game.Features.Games.AttributeSchema;
using DomainAttributeSpecification = DM.Domain.Game.Features.Games.AttributeSpecification;
using DomainListValue = DM.Domain.Game.Features.Games.ListValue;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <inheritdoc />
internal class AttributeSchemaMappingProfile : Profile
{
    /// <inheritdoc />
    public AttributeSchemaMappingProfile()
    {
        CreateMap<DomainAttributeSchema, AttributeSchema>()
            .ReverseMap();

        CreateMap<DomainAttributeSpecification, AttributeSpecification>()
            .ReverseMap();

        CreateMap<DomainListValue, AttributeValueSpecification>()
            .ReverseMap();
    }
}
