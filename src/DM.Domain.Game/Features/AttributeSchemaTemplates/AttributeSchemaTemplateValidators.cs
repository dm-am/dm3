using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

internal class CreateAttributeSchemaTemplateValidator : AbstractValidator<CreateAttributeSchemaTemplate>
{
    public CreateAttributeSchemaTemplateValidator()
    {
        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        this.RuleForSpecifications(t => t.Specifications);
    }
}

/// <remarks>
/// The creation rules: a template that cannot be created cannot be arrived at by replacing one.
/// </remarks>
internal class ReplaceAttributeSchemaTemplateValidator : AbstractValidator<ReplaceAttributeSchemaTemplate>
{
    public ReplaceAttributeSchemaTemplateValidator()
    {
        RuleFor(t => t.TemplateId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        Include(new CreateAttributeSchemaTemplateValidator());
    }
}
