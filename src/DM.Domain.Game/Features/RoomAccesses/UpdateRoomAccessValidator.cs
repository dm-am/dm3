using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <inheritdoc />
internal class UpdateRoomAccessValidator : AbstractValidator<UpdateRoomAccess>
{
    public UpdateRoomAccessValidator()
    {
        RuleFor(c => c.AccessId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // NoAccess is the deletion of the grant, not a value of it.
        RuleFor(c => c.Policy)
            .IsInEnum().WithMessage(ValidationError.Invalid)
            .Must(c => c != RoomAccessPolicy.NoAccess).WithMessage(ValidationError.Invalid);
    }
}
