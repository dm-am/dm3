using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Validator for attribute schema creation DTO
/// </summary>
internal class CreateAttributeSchemaValidator : AbstractValidator<CreateAttributeSchema>
{
    /// <inheritdoc />
    public CreateAttributeSchemaValidator()
    {
        RuleFor(s => s.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long);

        RuleFor(s => s.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleForEach(s => s.Specifications)
            .ChildRules(spec =>
            {
                spec.RuleFor(s => s.Title)
                    .NotEmpty().WithMessage(ValidationError.Empty)
                    .MaximumLength(100).WithMessage(ValidationError.Long);

                spec.RuleFor(s => s.Type)
                    .IsInEnum().WithMessage(ValidationError.Invalid);

                spec.RuleFor(s => s.Order)
                    .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
            });

        RuleFor(s => s.Specifications)
            .Custom((specifications, context) =>
            {
                foreach (var error in AttributeSpecificationRules.Collect(specifications))
                {
                    context.AddFailure(error);
                }
            });
    }
}
