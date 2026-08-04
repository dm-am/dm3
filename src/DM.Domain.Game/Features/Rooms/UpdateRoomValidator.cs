using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Rooms;

/// <inheritdoc />
internal class UpdateRoomValidator : AbstractValidator<UpdateRoom>
{
    /// <inheritdoc />
    public UpdateRoomValidator()
    {
        RuleFor(r => r.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(r => r.Title != default, () =>
            RuleFor(r => r.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(100).WithMessage(ValidationError.Long));
    }
}
