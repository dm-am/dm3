using DM.Services.Core.Exceptions;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using FluentValidation;

namespace DM.Services.Game.Dto.Input;

/// <inheritdoc />
internal class CreatePostPendencyValidator : AbstractValidator<CreatePostPendency>
{
    /// <inheritdoc />
    public CreatePostPendencyValidator(
        IUserRepository userRepository)
    {
        RuleFor(e => e.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(e => e.CharacterId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(e => e.WaitingForUserLogin)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(userRepository.UserExists).WithMessage(ValidationError.Invalid);
    }
}
