using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Characters;

internal interface IAttributeValueValidator
{
    (bool valid, string? error) Validate(string value, AttributeSpecification specification);
}
