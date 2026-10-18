using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <summary>
/// Validator for the replacement of a game's character sheet
/// </summary>
/// <remarks>
/// Everything here is answered before the game is even looked up: a body the
/// server cannot store is refused the same way whether or not the caller could
/// have edited that game.
/// </remarks>
internal class ReplaceGameAttributeSchemaValidator : AbstractValidator<ReplaceGameAttributeSchema>
{
    /// <inheritdoc />
    public ReplaceGameAttributeSchemaValidator()
    {
        RuleFor(s => s.GameId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // A caller without a version never read the sheet it is replacing, and
        // the replacement deletes values. Missing is refused rather than read as
        // "whatever is stored".
        RuleFor(s => s.Version)
            .NotNull().WithMessage(ValidationError.Empty);

        RuleForEach(s => s.Specifications)
            .ChildRules(AttributeSpecificationRules.Apply);

        RuleFor(s => s.Specifications)
            .Custom(AttributeSpecificationRules.AddFailures);

        RuleFor(s => s.Specifications)
            .Custom((specifications, context) => AttributeSpecificationRules.AddRepeatedIdentifierFailures(
                specifications?.Select(specification => specification.Id), context));
    }
}
