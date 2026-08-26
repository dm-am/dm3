using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Validator for tag group update
/// </summary>
internal class UpdateTagGroupValidator : AbstractValidator<UpdateTagGroup>
{
    public UpdateTagGroupValidator()
    {
        RuleFor(g => g.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

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
