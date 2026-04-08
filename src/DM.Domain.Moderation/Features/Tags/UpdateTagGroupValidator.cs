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
            .MaximumLength(100).WithMessage(ValidationError.Long);

        RuleFor(g => g.Description)
            .MaximumLength(500).WithMessage(ValidationError.Long);

        RuleFor(g => g.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
