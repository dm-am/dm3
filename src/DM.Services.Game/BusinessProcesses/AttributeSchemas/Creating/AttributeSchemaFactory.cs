using System;
using System.Linq;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;
using DM.Services.Game.Dto.Shared;
using AttributeSchema = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSchema;
using AttributeSpecification = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSpecification;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;

/// <inheritdoc />
internal class AttributeSchemaFactory : IAttributeSchemaFactory
{
    private readonly IGuidFactory _guidFactory;

    /// <inheritdoc />
    public AttributeSchemaFactory(
        IGuidFactory guidFactory)
    {
        _guidFactory = guidFactory;
    }
        
    /// <inheritdoc />
    public AttributeSchema CreateNew(Dto.Shared.AttributeSchema schema, Guid userId)
    {
        return new AttributeSchema
        {
            Id = _guidFactory.Create(),
            Title = schema.Title.Trim(),
            UserId = userId,
            Type = SchemaType.Private,
            IsRemoved = false,
            Specifications = schema.Specifications.Select(s => new AttributeSpecification
            {
                Id = _guidFactory.Create(),
                Title = s.Title.Trim(),
                Constraints = CreateConstraints(s)
            })
        };
    }

    /// <inheritdoc />
    public AttributeSchema CreateToUpdate(Dto.Shared.AttributeSchema schema, Guid userId)
    {
        return new AttributeSchema
        {
            Id = schema.Id,
            Title = schema.Title.Trim(),
            UserId = userId,
            Type = schema.Type,
            IsRemoved = false,
            Specifications = schema.Specifications.Select(s => new AttributeSpecification
            {
                Id = s.Id != Guid.Empty ? s.Id : _guidFactory.Create(),
                Title = s.Title.Trim(),
                Constraints = CreateConstraints(s)
            })
        };
    }

    private static AttributeConstraints CreateConstraints(Dto.Shared.AttributeSpecification specification)
    {
        return specification.Type switch
        {
            AttributeSpecificationType.Number => new NumberAttributeConstraints
            {
                Required = specification.Required,
                MinValue = specification.MinValue,
                MaxValue = specification.MaxValue
            },
            AttributeSpecificationType.String => new StringAttributeConstraints
            {
                Required = specification.Required, MaxLength = specification.MaxLength ?? 0
            },
            AttributeSpecificationType.List => new ListAttributeConstraints
            {
                Required = specification.Required,
                Values = specification.Values.Select(v =>
                    new ListAttributeValue {Value = v.Value, Modifier = v.Modifier})
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}