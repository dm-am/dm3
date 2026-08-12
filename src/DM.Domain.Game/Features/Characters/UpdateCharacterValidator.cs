using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class UpdateCharacterValidator : AbstractValidator<UpdateCharacter>
{
    private const string ErrorMessage = nameof(ErrorMessage);
    private const string SchemaCacheKey = nameof(SchemaCacheKey);

    /// <inheritdoc />
    public UpdateCharacterValidator(
        ICharacterRepository characterRepository,
        IAttributeValueValidator attributeValueValidator)
    {
        When(c => c.Name != default, () =>
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(CharacterPolicy.NameMaxLength).WithMessage(ValidationError.Long));

        // Same invariant as on create: one value per specification. Without it
        // a repeated identifier became a second insert and, since the unique
        // index exists, a failed save.
        RuleFor(c => c.Attributes)
            .Custom((attributes, context) =>
            {
                foreach (var error in CharacterAttributeRules.Collect(attributes))
                {
                    context.AddFailure(error);
                }
            });

        // Gated on the game having a schema, exactly as on create. A game without
        // one has no specifications to read, and asking for them anyway threw out
        // of the validator — the identifier of the schema is null on the game row,
        // and dereferencing it made a request that merely carries attributes the
        // game does not use answer 500 instead of being validated at all.
        WhenAsync(async (c, ct) => c.Attributes != null && c.Attributes.Any() &&
                                   await characterRepository.CharacterRequiresAttributes(c.CharacterId, ct), () =>
            RuleForEach(c => c.Attributes)
                .MustAsync(async (c, attribute, context, _) =>
                {
                    if (!context.RootContextData.TryGetValue(SchemaCacheKey, out var schemaWrapper) ||
                        schemaWrapper is not Dictionary<Guid, AttributeSpecification> specifications)
                    {
                        var schema = await characterRepository.GetCharacterSchema(c.CharacterId);
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
                .WithMessage($"{{{ErrorMessage}}}"));
    }
}
