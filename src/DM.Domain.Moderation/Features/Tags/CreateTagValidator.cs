using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Validator for tag creation
/// </summary>
internal class CreateTagValidator : AbstractValidator<CreateTag>
{
    public CreateTagValidator()
    {
        RuleFor(t => t.GroupId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TagFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Description)
            .MaximumLength(TagFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
