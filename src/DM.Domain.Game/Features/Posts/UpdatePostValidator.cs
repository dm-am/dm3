using DM.Domain.Core.Content;
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
        {
            RuleFor(p => p.GameText)
                .NotEmpty().WithMessage(ValidationError.Empty);

            // An unclosed or nested [private] renders as private and indexes as
            // public — see PrivateBlockMarkup.IsBalanced. Refused on the edit path
            // as well as on creation: an edit is how most of them would arrive.
            RuleFor(p => p.GameText)
                .Must(PrivateBlockMarkup.IsBalanced).WithMessage(ValidationError.Invalid);
        });
    }
}
