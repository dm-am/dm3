using System;
using AutoMapper;
using DomainAttributeSchema = DM.Domain.Game.Features.Games.AttributeSchema;
using DomainAttributeSpecification = DM.Domain.Game.Features.Games.AttributeSpecification;
using DomainListValue = DM.Domain.Game.Features.Games.ListValue;
using DomainCreateSchema = DM.Domain.Game.Features.Games.CreateAttributeSchema;
using DomainCreateSpecification = DM.Domain.Game.Features.Games.CreateAttributeSpecification;
using DomainUpdateSchema = DM.Domain.Game.Features.Games.UpdateAttributeSchema;
using DomainUpdateSpecification = DM.Domain.Game.Features.Games.UpdateAttributeSpecification;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <inheritdoc />
internal class AttributeSchemaMappingProfile : Profile
{
    /// <inheritdoc />
    public AttributeSchemaMappingProfile()
    {
        // Read path
        CreateMap<DomainAttributeSchema, AttributeSchema>()
            .ReverseMap();

        CreateMap<DomainAttributeSpecification, AttributeSpecification>()
            .ReverseMap();

        CreateMap<DomainListValue, AttributeValueSpecification>()
            .ReverseMap();

        // Write path (API DTO -> domain create/update DTOs)
        CreateMap<AttributeSchema, DomainCreateSchema>();
        CreateMap<AttributeSpecification, DomainCreateSpecification>();

        CreateMap<AttributeSchema, DomainUpdateSchema>()
            .ForMember(d => d.SchemaId, o => o.Ignore());
        CreateMap<AttributeSpecification, DomainUpdateSpecification>()
            // A zero Guid denotes a brand new specification (no persisted id yet)
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id == Guid.Empty ? (Guid?)null : s.Id));

        CreateMap<AttributeValueSpecification, DomainListValue>();
    }
}
