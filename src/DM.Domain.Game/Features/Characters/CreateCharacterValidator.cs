using System.Linq;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

internal class CreateCharacterValidator : AbstractValidator<CreateCharacter>
{
    public CreateCharacterValidator(
        ICharacterRepository characterRepository,
        IAttributeValueValidator attributeValueValidator)
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CharacterPolicy.NameMaxLength).WithMessage(ValidationError.Long);

        RuleFor(c => c.Attributes)
            .Custom(CharacterAttributeRules.AddFailures);

        // Every game has a sheet; an empty one requires nothing and accepts nothing.
        RuleFor(c => c.Attributes)
            .MustAsync(async (c, _, context, _) =>
            {
                var specifications = await CharacterAttributeRules.Specifications(
                    context, () => characterRepository.GetGameSchema(c.GameId));

                // A set: a repeated identifier must fail validation, not throw.
                var submittedIds = c.Attributes.Select(a => a.Id).ToHashSet();
                var missingAttributes = specifications
                    .Where(s => s.Value.Required && !submittedIds.Contains(s.Key))
                    .Select(s => (s.Value.Id, s.Value.Title))
                    .ToArray();

                context.MessageFormatter.AppendArgument(CharacterAttributeRules.ErrorMessage,
                    AttributeValidationError.ManyRequiredMissing(missingAttributes));
                return !missingAttributes.Any();
            })
            .WithMessage($"{{{CharacterAttributeRules.ErrorMessage}}}");

        RuleForEach(c => c.Attributes)
            .MustAsync((c, attribute, context, _) => CharacterAttributeRules.FitsSheet(attribute, context,
                () => characterRepository.GetGameSchema(c.GameId), attributeValueValidator))
            .WithMessage($"{{{CharacterAttributeRules.ErrorMessage}}}");
    }
}
