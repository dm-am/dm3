using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for an answer in the conversation of a ticket
/// </summary>
internal class CreateTicketCommentValidator : AbstractValidator<CreateTicketComment>
{
    public CreateTicketCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        // Waiting for moderation is the author's turn and comes of his answer alone.
        RuleFor(c => c.Status)
            .Must(status => status is null or TicketStatus.WaitingForUser or TicketStatus.Closed or TicketStatus.Spam)
            .WithMessage(ValidationError.Invalid);

        // Closing a ticket or marking it as spam is an answer by itself.
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .When(c => !EndsTheTicket(c.Status));

        // As long as the text of the ticket (TicketTextLimits): the whole conversation
        // comes back with the ticket in one answer.
        RuleFor(c => c.Text)
            .MaximumLength(TicketTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // Stored, a text the renderer refuses renders as nothing (IBbCodeNestingLimit).
        RuleFor(c => c.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid)
            .When(c => !string.IsNullOrEmpty(c.Text));

        // The tag set of comments has no hiding block: the tag would be published around the lines.
        RuleFor(c => c.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(c => PrivateBlockMarkup.DescribeSurfaceRefusal(c.Text))
            .When(c => !string.IsNullOrEmpty(c.Text));
    }

    private static bool EndsTheTicket(TicketStatus? status) =>
        status is TicketStatus.Closed or TicketStatus.Spam;
}
