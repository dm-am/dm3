using DM.Domain.Core.Content;
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

        // The description is the hint surface, where a hidden block is erased
        // for every reader: refused with the reason, as on every surface that
        // has none (the editor's markup check names it in the same words).
        RuleFor(t => t.Description)
            .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Description));

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
