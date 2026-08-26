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
            .MaximumLength(RoomFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(c => c.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(c => c.AccessType)
            .IsInEnum().WithMessage(ValidationError.Invalid);
    }
}
