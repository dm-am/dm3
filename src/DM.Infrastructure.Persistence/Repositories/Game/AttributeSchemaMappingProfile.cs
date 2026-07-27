using System.Linq;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

using DbSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbListValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DtoAttributeSchema = DM.Domain.Game.Features.Games.AttributeSchema;
using DtoAttributeSpec = DM.Domain.Game.Features.Games.AttributeSpecification;
using DtoListValue = DM.Domain.Game.Features.Games.ListValue;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// AutoMapper profile for attribute schema mapping
/// </summary>
internal class AttributeSchemaMappingProfile : Profile
{
    public AttributeSchemaMappingProfile()
    {
        CreateMap<DbSchema, DtoAttributeSchema>()
            .ForMember(d => d.Author, opt => opt.Ignore())
            .ForMember(d => d.Description, opt => opt.Ignore());
        CreateMap<DbSpecification, DtoAttributeSpec>()
            .ConvertUsing<SpecificationConverter>();
        CreateMap<DbListValue, DtoListValue>();
    }
}

/// <summary>
/// Converter for AttributeSpecification from DB entity to DTO
/// </summary>
internal class SpecificationConverter : ITypeConverter<DbSpecification, DtoAttributeSpec>
{
    public DtoAttributeSpec Convert(DbSpecification source, DtoAttributeSpec destination, ResolutionContext context)
    {
        var result = new DtoAttributeSpec
        {
            Id = source.Id,
            Title = source.Title,
            Type = ResolveType(source),
            Required = source.Constraints.Required,
            Order = source.Order,
            IsDescriptor = source.IsDescriptor,
            IsHidden = source.IsHidden
        };

        switch (source.Constraints)
        {
            case NumberAttributeConstraints numberConstraints:
                result.MaxLength = numberConstraints.MaxLength;
                result.Values = [];
                return result;
            case StringAttributeConstraints stringConstraints:
                result.MaxLength = stringConstraints.MaxLength;
                result.Values = [];
                return result;
            case ListAttributeConstraints listConstraints:
                result.Values = listConstraints.Values.Select(v => new DtoListValue
                {
                    Value = v.Value,
                    Modifier = v.Modifier
                });
                return result;
            case BbCodeAttributeConstraints bbCodeConstraints:
                result.MaxLength = bbCodeConstraints.MaxLength;
                result.Values = [];
                return result;
            default:
                return result;
        }
    }

    private static AttributeSpecificationType ResolveType(DbSpecification source) =>
        source.Constraints switch
        {
            NumberAttributeConstraints _ => AttributeSpecificationType.Number,
            StringAttributeConstraints _ => AttributeSpecificationType.Text,
            ListAttributeConstraints { Kind: ListValueKind.Number } => AttributeSpecificationType.NumberList,
            ListAttributeConstraints { Kind: ListValueKind.TextNumber } => AttributeSpecificationType.TextNumberList,
            ListAttributeConstraints _ => AttributeSpecificationType.TextList,
            BbCodeAttributeConstraints _ => AttributeSpecificationType.BbCode,
            _ => AttributeSpecificationType.Text
        };
}
