using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Rooms;

/// <inheritdoc />
internal class CreateRoomValidator : AbstractValidator<CreateRoom>
{
    /// <inheritdoc />
    public CreateRoomValidator()
    {
        RuleFor(c => c.GameId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long);
    }
}