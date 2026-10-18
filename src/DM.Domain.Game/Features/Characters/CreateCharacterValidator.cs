using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Validator for character creation DTO
/// </summary>
internal class CreateCharacterValidator : AbstractValidator<CreateCharacter>
{
    private const string SchemaCacheKey = nameof(SchemaCacheKey);
    private const string ErrorMessage = nameof(ErrorMessage);

    /// <inheritdoc />
    public CreateCharacterValidator(
        ICharacterRepository characterRepository,
        IAttributeValueValidator attributeValueValidator)
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CharacterPolicy.NameMaxLength).WithMessage(ValidationError.Long);

        RuleFor(c => c.Attributes)
            .Custom((attributes, context) =>
            {
                foreach (var error in CharacterAttributeRules.Collect(attributes))
                {
                    context.AddFailure(error);
                }
            });

        // Nothing gates the rules below on the game having a sheet any more:
        // every game has one. An empty sheet requires nothing and accepts
        // nothing, which is what these two rules already say when the set of
        // specifications is empty.
        RuleFor(c => c.Attributes)
            .MustAsync(async (c, _, context, _) =>
            {
                var specifications = await Specifications(characterRepository, c.GameId, context);

                // A set, not a dictionary of the submitted values: only the
                // presence of an identifier is read here, and a repeated one
                // threw out of the validator as a 500 instead of failing it.
                var submittedIds = c.Attributes.Select(a => a.Id).ToHashSet();
                var missingAttributes = specifications
                    .Where(s => s.Value.Required && !submittedIds.Contains(s.Key))
                    .Select(s => (s.Value.Id, s.Value.Title))
                    .ToArray();

                context.MessageFormatter.AppendArgument(ErrorMessage,
                    AttributeValidationError.ManyRequiredMissing(missingAttributes));
                return !missingAttributes.Any();
            })
            .WithMessage($"{{{ErrorMessage}}}");

        RuleForEach(c => c.Attributes)
            .MustAsync(async (c, attribute, context, _) =>
            {
                var specifications = await Specifications(characterRepository, c.GameId, context);

                if (!specifications.TryGetValue(attribute.Id, out var specification))
                {
                    context.MessageFormatter.AppendArgument(ErrorMessage,
                        AttributeValidationError.InvalidSpecification);
                    return false;
                }

                var (valid, error) = attributeValueValidator.Validate(attribute.Value, specification);
                context.MessageFormatter.AppendArgument(ErrorMessage, error);
                return valid;
            })
            .WithMessage($"{{{ErrorMessage}}}");
    }

    /// <summary>
    /// The sheet of the game, read once per validated request.
    /// </summary>
    private static async Task<Dictionary<Guid, AttributeSpecification>> Specifications(
        ICharacterRepository characterRepository, Guid gameId, IValidationContext context)
    {
        if (context.RootContextData.TryGetValue(SchemaCacheKey, out var cached) &&
            cached is Dictionary<Guid, AttributeSpecification> specifications)
        {
            return specifications;
        }

        var schema = await characterRepository.GetGameSchema(gameId);
        specifications = schema.Specifications.ToDictionary(s => s.Id);
        context.RootContextData[SchemaCacheKey] = specifications;
        return specifications;
    }
}
