using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.AttributeSchemas;
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
            .MaximumLength(50).WithMessage(ValidationError.Long);

        // Outside the schema block on purpose: a game without a schema skips
        // every rule below, and the repeated identifier used to reach storage
        // as two rows for one specification.
        RuleFor(c => c.Attributes)
            .Custom((attributes, context) =>
            {
                foreach (var error in CharacterAttributeRules.Collect(attributes))
                {
                    context.AddFailure(error);
                }
            });

        WhenAsync(async (c, ct) => await characterRepository.GameRequiresAttributes(c.GameId, ct), () =>
        {
            RuleFor(c => c.Attributes)
                .MustAsync(async (c, _, context, _) =>
                {
                    if (!context.RootContextData.TryGetValue(SchemaCacheKey, out var schemaWrapper) ||
                        schemaWrapper is not Dictionary<Guid, AttributeSpecification> specifications)
                    {
                        var schema = await characterRepository.GetGameSchema(c.GameId);
                        specifications = schema.Specifications.ToDictionary(s => s.Id);
                        context.RootContextData[SchemaCacheKey] = specifications;
                    }

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
                    if (!context.RootContextData.TryGetValue(SchemaCacheKey, out var schemaWrapper) ||
                        schemaWrapper is not Dictionary<Guid, AttributeSpecification> specifications)
                    {
                        var schema = await characterRepository.GetGameSchema(c.GameId);
                        specifications = schema.Specifications.ToDictionary(s => s.Id);
                        context.RootContextData[SchemaCacheKey] = specifications;
                    }

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
        });
    }
}
