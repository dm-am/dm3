using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

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

        // The hint surface erases a hidden block for every reader, so it is refused with the reason.
        RuleFor(t => t.Description)
            .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Description));

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
