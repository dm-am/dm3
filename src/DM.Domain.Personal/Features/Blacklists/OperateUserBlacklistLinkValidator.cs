using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Personal.Features.Blacklists;

/// <inheritdoc />
internal class OperateUserBlacklistLinkValidator : AbstractValidator<OperateUserBlacklistLink>
{
    /// <inheritdoc />
    public OperateUserBlacklistLinkValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(r => r.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(userLookupService.UserExistsAsync).WithMessage(ValidationError.Invalid);
    }
}
