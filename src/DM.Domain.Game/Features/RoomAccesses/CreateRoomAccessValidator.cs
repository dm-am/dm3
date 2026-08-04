using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <inheritdoc />
internal class CreateRoomAccessValidator : AbstractValidator<CreateRoomAccess>
{
    /// <inheritdoc />
    public CreateRoomAccessValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(c => c.Policy)
            .Must(p => p != RoomAccessPolicy.NoAccess).WithMessage(ValidationError.Invalid);

        When(c => c.CharacterId.HasValue, () =>
            RuleFor(c => c.CharacterId!.Value)
                .NotEmpty().WithMessage(ValidationError.Empty));

        // A reader row takes the same two policies a character row does: ReadOnly
        // seats a spectator, Full seats somebody who speaks in the room's chat.
        // Pinning it to ReadOnly left the column with no decision to carry for half
        // the rows in the table, and the authorization that reads it would have had
        // one answer for every reader in the product.
        When(c => !string.IsNullOrEmpty(c.ReaderUsername), () =>
            RuleFor(c => c.ReaderUsername)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MustAsync(userLookupService.UserExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
