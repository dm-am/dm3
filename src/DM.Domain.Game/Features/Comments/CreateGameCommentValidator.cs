using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Comments;

/// <inheritdoc />
internal class CreateGameCommentValidator : AbstractValidator<CreateGameComment>
{
    /// <inheritdoc />
    public CreateGameCommentValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
