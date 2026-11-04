using System.Linq;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.Chats;

/// <inheritdoc />
internal class CreateChatValidator : AbstractValidator<CreateChat>
{
    public CreateChatValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(ChatFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(c => c.ParticipantIds)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must(p => p.Count() <= ChatFieldLimits.MaxParticipants).WithMessage(ValidationError.TooMany);
    }
}
