using System.Linq;
using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <inheritdoc />
internal class CreateConversationValidator : AbstractValidator<CreateConversation>
{
    /// <inheritdoc />
    public CreateConversationValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(200).WithMessage(ValidationError.Long);

        RuleFor(c => c.ParticipantIds)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must(p => p.Count() <= 50).WithMessage(ValidationError.TooMany);
    }
}
