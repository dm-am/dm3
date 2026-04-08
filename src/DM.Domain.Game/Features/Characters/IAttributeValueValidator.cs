using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Characters;
/// <summary>
/// Validator for character attribute value
/// </summary>
internal interface IAttributeValueValidator
{
    /// <summary>
    /// Validate value against specification
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="specification">Attribute specification</param>
    /// <returns>Validation result with optional error message</returns>
    (bool valid, string? error) Validate(string value, AttributeSpecification specification);
}
