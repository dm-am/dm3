using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class UpdateMessageValidator : AbstractValidator<UpdateMessage>
{
    /// <inheritdoc />
    public UpdateMessageValidator()
    {
        RuleFor(m => m.MessageId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(m => m.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(50000).WithMessage(ValidationError.Long)
            .When(m => m.Text != null);
    }
}
