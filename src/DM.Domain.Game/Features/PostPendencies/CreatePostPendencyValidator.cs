using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.PostPendencies;

/// <inheritdoc />
internal class CreatePostPendencyValidator : AbstractValidator<CreatePostPendency>
{
    /// <inheritdoc />
    public CreatePostPendencyValidator()
    {
        RuleFor(e => e.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(e => e.CharacterId)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
