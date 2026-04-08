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
            .MaximumLength(100).WithMessage(ValidationError.Long);

        RuleFor(g => g.Description)
            .MaximumLength(500).WithMessage(ValidationError.Long);

        RuleFor(g => g.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
