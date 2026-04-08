using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Validator for attribute schema update DTO
/// </summary>
internal class UpdateAttributeSchemaValidator : AbstractValidator<UpdateAttributeSchema>
{
    /// <inheritdoc />
    public UpdateAttributeSchemaValidator()
    {
        RuleFor(s => s.SchemaId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(s => s.Title != null, () =>
            RuleFor(s => s.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(100).WithMessage(ValidationError.Long));

        When(s => s.Type.HasValue, () =>
            RuleFor(s => s.Type)
                .IsInEnum().WithMessage(ValidationError.Invalid));

        When(s => s.Specifications != null, () =>
            RuleForEach(s => s.Specifications)
                .ChildRules(spec =>
                {
                    spec.RuleFor(s => s.Title)
                        .NotEmpty().WithMessage(ValidationError.Empty)
                        .MaximumLength(100).WithMessage(ValidationError.Long);
                }));
    }
}
