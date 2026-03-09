using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <inheritdoc />
internal class UpdateRoomAccessValidator : AbstractValidator<UpdateRoomAccess>
{
    /// <inheritdoc />
    public UpdateRoomAccessValidator()
    {
        RuleFor(c => c.AccessId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Policy)
            .Must(c => c != RoomAccessPolicy.NoAccess).WithMessage(ValidationError.Invalid);
    }
}