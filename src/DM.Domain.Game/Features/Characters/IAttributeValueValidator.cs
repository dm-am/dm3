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
    /// <param name="value"></param>
    /// <param name="specification"></param>
    /// <returns></returns>
    (bool valid, string? error) Validate(string value, AttributeSpecification specification);
}
