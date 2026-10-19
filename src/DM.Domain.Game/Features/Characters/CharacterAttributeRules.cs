using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.GameAttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Rules for submitted attribute values, shared by the create and update validators.
/// </summary>
internal static class CharacterAttributeRules
{
    /// <summary>
    /// The message argument the value rules write their refusal into.
    /// </summary>
    public const string ErrorMessage = nameof(ErrorMessage);

    private const string SchemaCacheKey = nameof(SchemaCacheKey);

    /// <param name="attributes">Null on an update that leaves the values alone</param>
    public static IEnumerable<string> Collect(IEnumerable<CharacterAttribute>? attributes)
    {
        if (attributes == null)
        {
            yield break;
        }

        // Stored rows are unique on (CharacterId, AttributeId): one of two values would be lost.
        var duplicated = attributes
            .GroupBy(a => a.Id)
            .Where(g => g.Skip(1).Any())
            .Select(g => g.Key)
            .ToArray();

        if (duplicated.Length > 0)
        {
            yield return AttributeValidationError.ManyDuplicated(duplicated);
        }
    }

    public static void AddFailures<T>(IEnumerable<CharacterAttribute>? attributes, ValidationContext<T> context)
    {
        foreach (var error in Collect(attributes))
        {
            context.AddFailure(error);
        }
    }

    /// <summary>
    /// The sheet of the game, read once per validated request.
    /// </summary>
    public static async Task<Dictionary<Guid, AttributeSpecification>> Specifications(
        IValidationContext context, Func<Task<GameAttributeSchema>> readSchema)
    {
        if (context.RootContextData.TryGetValue(SchemaCacheKey, out var cached) &&
            cached is Dictionary<Guid, AttributeSpecification> specifications)
        {
            return specifications;
        }

        specifications = (await readSchema()).Specifications.ToDictionary(s => s.Id);
        context.RootContextData[SchemaCacheKey] = specifications;
        return specifications;
    }

    /// <summary>
    /// The sheet holds a specification for the value and the value fits it.
    /// </summary>
    public static async Task<bool> FitsSheet<T>(CharacterAttribute attribute, ValidationContext<T> context,
        Func<Task<GameAttributeSchema>> readSchema, IAttributeValueValidator attributeValueValidator)
    {
        var specifications = await Specifications(context, readSchema);
        if (!specifications.TryGetValue(attribute.Id, out var specification))
        {
            context.MessageFormatter.AppendArgument(ErrorMessage, AttributeValidationError.InvalidSpecification);
            return false;
        }

        var (valid, error) = attributeValueValidator.Validate(attribute.Value, specification);
        context.MessageFormatter.AppendArgument(ErrorMessage, error);
        return valid;
    }
}
