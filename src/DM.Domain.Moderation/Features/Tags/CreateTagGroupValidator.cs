using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

internal class CreateTagGroupValidator : AbstractValidator<CreateTagGroup>
{
    public CreateTagGroupValidator()
    {
        RuleFor(g => g.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TagFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(g => g.Description)
            .MaximumLength(TagFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

        RuleFor(g => g.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);

        // No limit is null, not zero: nobody could pick a tag from such a group.
        RuleFor(g => g.MaxTagsPerGame)
            .GreaterThanOrEqualTo(1).WithMessage(ValidationError.Invalid);
    }
}
