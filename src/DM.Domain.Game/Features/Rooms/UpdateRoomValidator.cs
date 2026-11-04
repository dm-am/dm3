using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Rooms;

/// <inheritdoc />
internal class UpdateRoomValidator : AbstractValidator<UpdateRoom>
{
    public UpdateRoomValidator()
    {
        RuleFor(r => r.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(r => r.Title != default, () =>
            RuleFor(r => r.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(RoomFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long));

        // An integer outside the enum would store a room of no known type; null keeps the stored value.
        RuleFor(r => r.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(r => r.AccessType)
            .IsInEnum().WithMessage(ValidationError.Invalid);
    }
}
