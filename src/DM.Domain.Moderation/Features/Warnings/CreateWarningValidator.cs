using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Warnings;

internal class CreateWarningValidator : AbstractValidator<CreateWarning>
{
    public CreateWarningValidator()
    {
        RuleFor(w => w.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(w => w.Points)
            .InclusiveBetween(0, WarningPolicy.MaxPointsPerWarning).WithMessage(ValidationError.Invalid);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(w => w.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(WarningPolicy.ReasonMaxLength).WithMessage(ValidationError.Long)
            .Must(reason => !PrivateBlockMarkup.ContainsPrivateMarkup(reason))
            .WithMessage(w => PrivateBlockMarkup.DescribeSurfaceRefusal(w.Reason));

        RuleFor(w => w.EntityType)
            .MaximumLength(100).WithMessage(ValidationError.Long);
    }
}
