using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class CharacterAttributeValueFiller : ICharacterAttributeValueFiller
{
    private readonly IAttributeSchemaService _schemaService;
    private readonly IAttributeValueValidator _attributeValueValidator;

    /// <inheritdoc />
    public CharacterAttributeValueFiller(
        IAttributeSchemaService schemaService,
        IAttributeValueValidator attributeValueValidator)
    {
        _schemaService = schemaService;
        _attributeValueValidator = attributeValueValidator;
    }

    public async Task Fill(IEnumerable<Character> characters, Games.Game game, Guid viewerId)
    {
        var schemaId = game.AttributeSchemaId;
        if (!schemaId.HasValue)
        {
            foreach (var character in characters)
            {
                character.Attributes = [];
            }
            return;
        }

        // Leads (master/assistant) see every hidden value in the game — same
        // lead-override the [private] contract uses. Mentors/moderators do not.
        var viewerIsLead = game.GetRoles(viewerId).HasEditAccess();

        var schema = await _schemaService.GetAsync(schemaId.Value);
        // At most one spec per schema is the descriptor ("show on game main page").
        var descriptorSpec = schema.Specifications.FirstOrDefault(s => s.IsDescriptor);
        foreach (var character in characters)
        {
            // The character owner always sees their own hidden values.
            var canSeeHidden = viewerIsLead ||
                (character.Author != null && character.Author.UserId == viewerId);

            var attributeIndex = character.Attributes.ToDictionary(a => a.Id);

            // Descriptor value ("Класс") for the game main-page roster. Read from
            // the raw attribute values (independent of hidden-spec redaction);
            // stays null when the schema has no descriptor or the value is empty.
            if (descriptorSpec != null &&
                attributeIndex.TryGetValue(descriptorSpec.Id, out var descriptorAttribute) &&
                !string.IsNullOrWhiteSpace(descriptorAttribute.Value))
            {
                character.Descriptor = descriptorAttribute.Value;
            }

            var filledAttributes = new List<CharacterAttribute>(character.Attributes.Count());

            foreach (var specification in schema.Specifications)
            {
                // Privacy redaction: drop hidden specs (and their values)
                // entirely for viewers who may not see them. Done in the domain,
                // before the DTO is built, so redacted values never leave the
                // server.
                if (specification.IsHidden && !canSeeHidden)
                {
                    continue;
                }

                if (!attributeIndex.TryGetValue(specification.Id, out var attribute))
                {
                    filledAttributes.Add(new CharacterAttribute
                    {
                        Id = specification.Id,
                        Title = specification.Title,
                        Type = specification.Type,
                        Modifier = null,
                        Value = string.Empty,
                        Inconsistent = true
                    });
                }
                else
                {
                    var (valid, _) = _attributeValueValidator.Validate(attribute.Value, specification);
                    var filledAttribute = new CharacterAttribute
                    {
                        Id = specification.Id,
                        Title = specification.Title,
                        Type = specification.Type,
                        Value = attribute.Value,
                        Inconsistent = !valid
                    };

                    if (specification.Type is AttributeSpecificationType.TextList
                        or AttributeSpecificationType.NumberList
                        or AttributeSpecificationType.TextNumberList)
                    {
                        var matchingValue = specification.Values.FirstOrDefault(v => v.Value == attribute.Value);
                        if (matchingValue != null)
                        {
                            filledAttribute.Modifier = matchingValue.Modifier;
                        }
                    }

                    filledAttributes.Add(filledAttribute);
                }
            }

            character.Attributes = filledAttributes;
        }
    }
}
