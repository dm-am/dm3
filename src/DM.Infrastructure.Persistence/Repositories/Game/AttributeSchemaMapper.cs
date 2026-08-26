using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DbSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DtoAttributeSchema = DM.Domain.Game.Features.Games.AttributeSchema;
using DtoAttributeSpec = DM.Domain.Game.Features.Games.AttributeSpecification;
using DtoListValue = DM.Domain.Game.Features.Games.ListValue;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// In-memory mapper for attribute schemata. The specifications live in a
/// JSON document on the schema row, so the whole conversion happens on the
/// loaded entity - the constraint subtype is what decides the specification
/// type and which limits it carries. The author is resolved separately by
/// the repository (the row keeps only the user id).
/// </summary>
internal static class AttributeSchemaMapper
{
    /// <summary>
    /// Loaded schema row to the domain DTO
    /// </summary>
    public static DtoAttributeSchema ToAttributeSchema(this DbSchema schema) => new()
    {
        Id = schema.AttributeSchemaId,
        Type = schema.Type,
        Title = schema.Title,
        Specifications = schema.Specifications.Select(ToSpecification).ToList()
    };

    private static DtoAttributeSpec ToSpecification(DbSpecification source)
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
