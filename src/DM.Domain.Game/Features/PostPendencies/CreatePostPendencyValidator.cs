using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.PostPendencies;

/// <inheritdoc />
internal class CreatePostPendencyValidator : AbstractValidator<CreatePostPendency>
{
    /// <inheritdoc />
    public CreatePostPendencyValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(e => e.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(e => e.CharacterId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(e => e.WaitingForUsername)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(userLookupService.UserExistsAsync).WithMessage(ValidationError.Invalid);
    }
}
