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
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(s => s.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleForEach(s => s.Specifications)
            .ChildRules(AttributeSpecificationRules.Apply);

        RuleFor(s => s.Specifications)
            .Custom(AttributeSpecificationRules.AddFailures);
    }
}
