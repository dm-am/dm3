using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DbSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeConstraints;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbNumberConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.NumberAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DtoSpecification = DM.Domain.Game.Features.Games.AttributeSpecification;
using DtoSpecificationInput = DM.Domain.Game.Features.Games.IAttributeSpecificationInput;
using DtoListValue = DM.Domain.Game.Features.Games.ListValue;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// The document of attribute specifications, in both directions.
/// </summary>
/// <remarks>
/// Two stores hold this document now, the schema of a game and the shared
/// schema row, and they have to read and write it the same way: the
/// constraint subclass is what carries the type of a specification and the
/// limits it declares, so a second spelling of the conversion is a second
/// answer to "what type is this attribute". One class, called by both.
/// </remarks>
internal static class AttributeSpecificationDocument
{
    /// <summary>
    /// Stored specifications to the domain shape.
    /// </summary>
    public static IEnumerable<DtoSpecification> ToSpecifications(
        IEnumerable<DbSpecification> specifications) =>
        specifications.Select(ToSpecification).ToList();

    /// <summary>
    /// Submitted specifications to the stored shape, each with the identifier
    /// the caller decided it keeps.
    /// </summary>
    public static List<DbSpecification> ToDocument<TSpecification>(
        IEnumerable<TSpecification> specifications,
        Func<TSpecification, Guid> identify)
        where TSpecification : DtoSpecificationInput =>
        specifications.Select(specification => new DbSpecification
        {
            Id = identify(specification),
            Title = specification.Title.Trim(),
            Order = specification.Order,
            IsDescriptor = specification.IsDescriptor,
            IsHidden = specification.IsHidden,
            Constraints = BuildConstraints(specification, specification.Required)
        }).ToList();

    /// <summary>
    /// Build strongly-typed persistence constraints from a write-side specification
    /// </summary>
    /// <remarks>
    /// Public rather than private so the round trip of an absent limit can be
    /// asserted end to end. The absence is written here and read back by
    /// <see cref="ToSpecification" />, and a test that sees only one of the two
    /// halves stays green while the other half turns the absence back into a
    /// zero.
    /// </remarks>
    public static DbConstraints BuildConstraints(DtoSpecificationInput spec, bool required) =>
        spec.Type switch
        {
            AttributeSpecificationType.Number =>
                new DbNumberConstraints { Required = required, MaxLength = spec.MaxLength },
            AttributeSpecificationType.TextList =>
                new DbListConstraints { Required = required, Kind = ListValueKind.Text, Values = MapValues(spec.Values) },
            AttributeSpecificationType.NumberList =>
                new DbListConstraints { Required = required, Kind = ListValueKind.Number, Values = MapValues(spec.Values) },
            AttributeSpecificationType.TextNumberList =>
                new DbListConstraints { Required = required, Kind = ListValueKind.TextNumber, Values = MapValues(spec.Values) },
            AttributeSpecificationType.BbCode =>
                new DbBbCodeConstraints { Required = required, MaxLength = spec.MaxLength },
            _ =>
                new DbStringConstraints { Required = required, MaxLength = spec.MaxLength }
        };

    private static List<DbListAttributeValue> MapValues(IEnumerable<DtoListValue> values) =>
        (values ?? []).Select(v => new DbListAttributeValue { Value = v.Value, Modifier = v.Modifier }).ToList();

    private static DtoSpecification ToSpecification(DbSpecification source)
    {
        var result = new DtoSpecification
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
