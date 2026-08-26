using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Validator for tag group creation
/// </summary>
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

        // Zero is not "no limit" - null is. A group nobody can pick a tag from
        // would be a group that may as well be deleted.
        RuleFor(g => g.MaxTagsPerGame)
            .GreaterThanOrEqualTo(1).WithMessage(ValidationError.Invalid)
            .When(g => g.MaxTagsPerGame.HasValue);
    }
}
