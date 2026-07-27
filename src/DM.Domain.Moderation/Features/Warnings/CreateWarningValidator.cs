using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Validator for warning creation
/// </summary>
internal class CreateWarningValidator : AbstractValidator<CreateWarning>
{
    public CreateWarningValidator()
    {
        RuleFor(w => w.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(w => w.Points)
            .InclusiveBetween(0, 6).WithMessage(ValidationError.Invalid);

        RuleFor(w => w.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);

        RuleFor(w => w.EntityType)
            .MaximumLength(100).WithMessage(ValidationError.Long)
            .When(w => w.EntityType != null);
    }
}
