using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class UpdatePostValidator : AbstractValidator<UpdatePost>
{
    /// <inheritdoc />
    public UpdatePostValidator()
    {
        RuleFor(p => p.PostId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        When(p => p.GameText != default, () =>
            RuleFor(p => p.GameText)
                .NotEmpty().WithMessage(ValidationError.Empty));
    }
}
