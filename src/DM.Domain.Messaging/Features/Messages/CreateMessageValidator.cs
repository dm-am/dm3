using DM.Domain.Core.Content;
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
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // One path for every chat: the global room, a direct message and a
        // message in a game room all arrive here, and none of the three surfaces
        // declares [private]. The tag is not markup on any of them, so it hides
        // nothing and the line is sent with the tag still around it.
        RuleFor(m => m.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(m => PrivateBlockMarkup.DescribeSurfaceRefusal(m.Text));
    }
}
