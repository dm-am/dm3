using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class CreatePostValidator : AbstractValidator<CreatePost>
{
    /// <inheritdoc />
    public CreatePostValidator()
    {
        RuleFor(p => p.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.Text)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}