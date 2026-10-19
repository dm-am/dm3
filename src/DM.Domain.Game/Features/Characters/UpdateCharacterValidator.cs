using System.Linq;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class UpdateCharacterValidator : AbstractValidator<UpdateCharacter>
{
    public UpdateCharacterValidator(
        ICharacterRepository characterRepository,
        IAttributeValueValidator attributeValueValidator)
    {
        When(c => c.Name is not null, () =>
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(CharacterPolicy.NameMaxLength).WithMessage(ValidationError.Long));

        RuleFor(c => c.Attributes)
            .Custom(CharacterAttributeRules.AddFailures);

        When(c => c.Attributes.Any(), () =>
            RuleForEach(c => c.Attributes)
                .MustAsync((c, attribute, context, _) => CharacterAttributeRules.FitsSheet(attribute, context,
                    () => characterRepository.GetCharacterSchema(c.CharacterId), attributeValueValidator))
                .WithMessage($"{{{CharacterAttributeRules.ErrorMessage}}}"));
    }
}
