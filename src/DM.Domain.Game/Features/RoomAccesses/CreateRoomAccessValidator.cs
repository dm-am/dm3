using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <inheritdoc />
internal class CreateRoomAccessValidator : AbstractValidator<CreateRoomAccess>
{
    public CreateRoomAccessValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(c => c.Policy)
            .IsInEnum().WithMessage(ValidationError.Invalid)
            .Must(p => p != RoomAccessPolicy.NoAccess).WithMessage(ValidationError.Invalid);

        When(c => c.CharacterId.HasValue, () =>
            RuleFor(c => c.CharacterId!.Value)
                .NotEmpty().WithMessage(ValidationError.Empty));

        // A reader row takes both policies, as a character row does: Full speaks in the room's chat.
        When(c => !string.IsNullOrEmpty(c.ReaderUsername), () =>
            RuleFor(c => c.ReaderUsername)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
