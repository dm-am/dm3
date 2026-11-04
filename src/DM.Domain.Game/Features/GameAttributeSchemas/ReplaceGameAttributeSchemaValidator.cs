using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

internal class ReplaceGameAttributeSchemaValidator : AbstractValidator<ReplaceGameAttributeSchema>
{
    public ReplaceGameAttributeSchemaValidator()
    {
        RuleFor(s => s.GameId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // The replacement deletes values, so a caller that never read the sheet is refused.
        RuleFor(s => s.Version)
            .NotNull().WithMessage(ValidationError.Empty);

        this.RuleForSpecifications(s => s.Specifications);

        RuleFor(s => s.Specifications)
            .Custom((specifications, context) => AttributeSpecificationRules.AddRepeatedIdentifierFailures(
                specifications?.Select(specification => specification.Id), context));
    }
}
