using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class CreateMessageValidator : AbstractValidator<CreateMessage>
{
    /// <inheritdoc />
    public CreateMessageValidator()
    {
        RuleFor(m => m.ChatId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(m => m.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(50000).WithMessage(ValidationError.Long);
    }
}
