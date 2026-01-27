using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class UpdateConversationValidator : AbstractValidator<UpdateConversation>
{
    /// <inheritdoc />
    public UpdateConversationValidator()
    {
        RuleFor(c => c.ConversationId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Title)
            .MaximumLength(200).WithMessage(ValidationError.Long)
            .When(c => c.Title != null);
    }
}
