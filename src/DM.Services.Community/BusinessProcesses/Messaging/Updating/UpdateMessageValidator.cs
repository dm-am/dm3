using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class UpdateMessageValidator : AbstractValidator<UpdateMessage>
{
    /// <inheritdoc />
    public UpdateMessageValidator()
    {
        RuleFor(m => m.MessageId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(m => m.Text)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
