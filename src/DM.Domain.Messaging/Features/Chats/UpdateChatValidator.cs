using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Chats;

/// <inheritdoc />
internal class UpdateChatValidator : AbstractValidator<UpdateChat>
{
    /// <inheritdoc />
    public UpdateChatValidator()
    {
        RuleFor(c => c.ChatId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Title)
            .MaximumLength(ChatFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long)
            .When(c => c.Title != null);
    }
}
