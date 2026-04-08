using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Blog.Features.Blacklists;

/// <inheritdoc />
internal class OperateBlogBlacklistLinkValidator : AbstractValidator<OperateBlogBlacklistLink>
{
    /// <inheritdoc />
    public OperateBlogBlacklistLinkValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(r => r.BlogId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(userLookupService.UserExistsAsync).WithMessage(ValidationError.Invalid);
    }
}
