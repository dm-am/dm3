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

        When(c => c.Attributes != null && c.Attributes.Any(), () =>
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
