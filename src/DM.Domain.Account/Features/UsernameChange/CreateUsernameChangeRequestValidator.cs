using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class CreateUsernameChangeRequestValidator : AbstractValidator<CreateUsernameChangeRequest>
{
    public CreateUsernameChangeRequestValidator()
    {
        // Only reason is required at creation time
        // Username is validated separately when completing the change after approval
        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(500).WithMessage(ValidationError.Long);
    }
}
