using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Game.Dto.Input;

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