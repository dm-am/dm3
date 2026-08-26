using DM.Domain.Core.Content;
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
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long)
            .When(m => m.Text != null);

        // No chat surface declares [private], and an edit is the other way the
        // tag gets into a stored message.
        RuleFor(m => m.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(m => PrivateBlockMarkup.DescribeSurfaceRefusal(m.Text))
            .When(m => m.Text != null);
    }
}
