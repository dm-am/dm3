using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.Blacklists;

/// <inheritdoc />
internal class OperateBlacklistLinkValidator : AbstractValidator<OperateBlacklistLink>
{
    /// <inheritdoc />
    public OperateBlacklistLinkValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(r => r.GameId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid);
    }
}
