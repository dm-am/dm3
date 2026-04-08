using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Validator for tag update
/// </summary>
internal class UpdateTagValidator : AbstractValidator<UpdateTag>
{
    public UpdateTagValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.GroupId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long);

        RuleFor(t => t.Description)
            .MaximumLength(500).WithMessage(ValidationError.Long);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}
