using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Messages;

/// <inheritdoc />
internal class CreateMessageValidator : AbstractValidator<CreateMessage>
{
    public CreateMessageValidator()
    {
        RuleFor(m => m.ChatId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(m => m.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // Every chat comes here, and no chat surface declares [private]: the tag would
        // hide nothing and be sent with the line.
        RuleFor(m => m.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(m => PrivateBlockMarkup.DescribeSurfaceRefusal(m.Text));
    }
}
