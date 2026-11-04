using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.GameAttributeSchemas;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class CharacterAttributeValueFiller : ICharacterAttributeValueFiller
{
    private readonly IGameAttributeSchemaRepository _gameSchemaRepository;
    private readonly IAttributeValueValidator _attributeValueValidator;

    public CharacterAttributeValueFiller(
        IGameAttributeSchemaRepository gameSchemaRepository,
        IAttributeValueValidator attributeValueValidator)
    {
        _gameSchemaRepository = gameSchemaRepository;
        _attributeValueValidator = attributeValueValidator;
    }

    public async Task Fill(IEnumerable<Character> characters, Games.Game game, Guid viewerId)
    {
        // The leads see hidden values, as with [private]; mentors and moderators do not.
        var viewerIsLead = game.GetRoles(viewerId).HasEditAccess();

        var schema = await _gameSchemaRepository.Get(game.Id);
        var descriptorSpec = schema.Specifications.FirstOrDefault(s => s.IsDescriptor);
        foreach (var character in characters)
        {
            var canSeeHidden = viewerIsLead || character.Author?.UserId == viewerId;

            var attributeIndex = character.Attributes.ToDictionary(a => a.Id);
            var filledAttributes = new List<CharacterAttribute>(character.Attributes.Count());

            foreach (var specification in schema.Specifications)
            {
                // Redacted here, before any DTO is built, so the value never leaves the server.
                if (specification.IsHidden && !canSeeHidden)
                {
                    continue;
                }

                filledAttributes.Add(FillAttribute(specification,
                    attributeIndex.TryGetValue(specification.Id, out var attribute) ? attribute : null));
            }

            character.Attributes = filledAttributes;

            // Read from the redacted row, so a sheet written around the validator hides it still.
            var descriptor = filledAttributes.FirstOrDefault(a => a.Id == descriptorSpec?.Id);
            if (!string.IsNullOrWhiteSpace(descriptor?.Value))
            {
                character.Descriptor = descriptor.Value;
            }
        }
    }

    /// <summary>
    /// The value checked against its specification, or an empty inconsistent one when absent.
    /// </summary>
    private CharacterAttribute FillAttribute(AttributeSpecification specification, CharacterAttribute? attribute)
    {
        if (attribute == null)
        {
            return new CharacterAttribute
            {
                Id = specification.Id,
                Title = specification.Title,
                Type = specification.Type,
                Value = string.Empty,
                Inconsistent = true
            };
        }

        var (valid, _) = _attributeValueValidator.Validate(attribute.Value, specification);
        var filledAttribute = new CharacterAttribute
        {
            Id = specification.Id,
            Title = specification.Title,
            Type = specification.Type,
            Value = attribute.Value,
            Inconsistent = !valid
        };

        if (AttributeSpecificationRules.IsListType(specification.Type))
        {
            var matchingValue = specification.Values.FirstOrDefault(v => v.Value == attribute.Value);
            if (matchingValue != null)
            {
                filledAttribute.Modifier = matchingValue.Modifier;
            }
        }

        return filledAttribute;
    }
}
