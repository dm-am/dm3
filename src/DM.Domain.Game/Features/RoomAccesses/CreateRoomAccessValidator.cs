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

        When(c => !string.IsNullOrEmpty(c.ReaderUsername), () =>
        {
            RuleFor(c => c.ReaderUsername)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MustAsync(userLookupService.UserExistsAsync).WithMessage(ValidationError.Invalid);
            RuleFor(c => c.Policy)
                .Must(c => c == RoomAccessPolicy.ReadOnly).WithMessage(ValidationError.Invalid);
        });
    }
}