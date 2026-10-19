using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class UpdateMessageValidator : AbstractValidator<UpdateMessage>
{
    public UpdateMessageValidator()
    {
        RuleFor(m => m.MessageId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(m => m.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long)
            .When(m => m.Text != null);

        // An edit is the other way the tag gets into a stored message (CreateMessageValidator).
        RuleFor(m => m.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(m => PrivateBlockMarkup.DescribeSurfaceRefusal(m.Text));
    }
}
