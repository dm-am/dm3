using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <summary>
/// Validator for the creation of an attribute schema template
/// </summary>
internal class CreateAttributeSchemaTemplateValidator : AbstractValidator<CreateAttributeSchemaTemplate>
{
    /// <inheritdoc />
    public CreateAttributeSchemaTemplateValidator()
    {
        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleForEach(t => t.Specifications)
            .ChildRules(AttributeSpecificationRules.Apply);

        RuleFor(t => t.Specifications)
            .Custom(AttributeSpecificationRules.AddFailures);
    }
}

/// <summary>
/// Validator for the replacement of an attribute schema template
/// </summary>
/// <remarks>
/// The same rules as the creation, over the whole set: a replacement writes
/// every field, so a template that cannot be created cannot be arrived at by
/// editing one either.
/// </remarks>
internal class ReplaceAttributeSchemaTemplateValidator : AbstractValidator<ReplaceAttributeSchemaTemplate>
{
    /// <inheritdoc />
    public ReplaceAttributeSchemaTemplateValidator()
    {
        RuleFor(t => t.TemplateId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleForEach(t => t.Specifications)
            .ChildRules(AttributeSpecificationRules.Apply);

        RuleFor(t => t.Specifications)
            .Custom(AttributeSpecificationRules.AddFailures);
    }
}
