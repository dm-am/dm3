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

        // Both columns are read back by comparing against the named members, so
        // an integer outside the enum is stored as a room that is of no known
        // type and of no known access. Null passes: omitted keeps the stored value.
        RuleFor(r => r.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(r => r.AccessType)
            .IsInEnum().WithMessage(ValidationError.Invalid);
    }
}
