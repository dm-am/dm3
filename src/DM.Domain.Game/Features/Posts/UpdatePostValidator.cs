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
        When(p => p.Text != default, () =>
            RuleFor(p => p.Text)
                .NotEmpty().WithMessage(ValidationError.Empty));
    }
}